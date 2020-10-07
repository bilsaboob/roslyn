// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class OverloadResolution
    {
        // During overload resolution we need to map arguments to their corresponding 
        // parameters, but most of the time that map is going to be trivial:
        // argument 0 corresponds to parameter 0, argument 1 corresponds to parameter 1,
        // and so on. Only when the call involves named arguments, optional parameters or
        // expanded form params methods is that not the case.
        //
        // To avoid the GC pressure of allocating a lot of unnecessary trivial maps,
        // we have this immutable struct which maintains the map. If the mapping is
        // trivial then no array is ever allocated.

        private struct ParameterMap
        {
            private readonly int[] _parameters;
            private readonly int _length;

            public ParameterMap(int[] parameters, int length)
            {
                Debug.Assert(parameters == null || parameters.Length == length);
                _parameters = parameters;
                _length = length;
            }

            public bool IsTrivial { get { return _parameters == null; } }

            public int Length { get { return _length; } }

            public int this[int argument]
            {
                get
                {
                    Debug.Assert(0 <= argument && argument < _length);
                    return _parameters == null ? argument : _parameters[argument];
                }
            }

            public ImmutableArray<int> ToImmutableArray()
            {
                return _parameters.AsImmutableOrNull();
            }
        }

        private ArgumentAnalysisResult AnalyzeArguments(
            Symbol symbol,
            AnalyzedArguments arguments,
            bool isMethodGroupConversion,
            bool expanded)
        {
            Debug.Assert((object)symbol != null);
            Debug.Assert(arguments != null);

            ImmutableArray<ParameterSymbol> parameters = symbol.GetParameters();
            bool isVararg = symbol.GetIsVararg();

            // We simulate an additional non-optional parameter for a vararg method.

            int argumentCount = arguments.Arguments.Count;
            var parameterCount = parameters.Length;

            int[] parametersPositions = null;
            int? unmatchedArgumentIndex = null;
            bool? unmatchedArgumentIsNamed = null;
            var parameterPosition = 0;
            var hasAnyMatchedSpreadArgs = false;

            void mapParameter(int argPos, int paramPos)
            {
                // fill the parameter positions
                var isFirstMapping = parametersPositions == null;
                parametersPositions ??= new int[argumentCount];

                if (paramPos != argPos || isFirstMapping)
                {
                    for (var i = 0; i < argPos; ++i)
                    {
                        var pos = isFirstMapping ? i : parametersPositions[i];
                        parametersPositions[i] = pos;
                    }

                    for (var i = argPos; i < argumentCount; ++i)
                    {
                        parametersPositions[i] = -1;
                    }
                }

                parametersPositions[argPos] = paramPos;
            }

            void moveToNextParameterPosition()
            {
                if (parameterPosition >= parameterCount) return;

                parameterPosition += 1;

                // check if already mapped to some arg
                var isMapped = false;
                var highestNext = 0;

                if (parametersPositions != null)
                {
                    for (int i = 0; i < argumentCount; ++i)
                    {
                        var p = parametersPositions[i];
                        if (p == parameterPosition)
                            isMapped = true;

                        if (p > highestNext)
                            highestNext = p;
                    }
                }

                if (isMapped)
                    parameterPosition = highestNext + 1;
            }

            // Try to map every argument position to a formal parameter position
            for (int argumentPosition = 0; argumentPosition < argumentCount; ++argumentPosition)
            {
                // check for explicit named parameter
                var namedParameterPosition = TryGetNamedParameterPosition(parameters, arguments, argumentPosition, out var isNamed, out var isSpread);
                if (namedParameterPosition != null || isNamed)
                {
                    // no additional checks required... we expect the user to know what they are doing when explicitly naming arguments
                    var namedParamPos = namedParameterPosition ?? -1;
                    mapParameter(argumentPosition, namedParamPos);

                    // set this as the first unmatched argument if it's named and no position available - no parameter with the specified name exist!
                    if (namedParameterPosition == null && unmatchedArgumentIndex == null)
                    {
                        unmatchedArgumentIndex = argumentPosition;
                        unmatchedArgumentIsNamed = true;
                    }
                    else
                    {
                        if (isSpread) hasAnyMatchedSpreadArgs = true;
                    }

                    // move to the next valid parameter to be evaluated next - it must be the one after the named one...
                    // NOTE: we can NOT have a named parameter and then expect the "required non named" parameters to follow... the following args will be mapped to the parameter following the named one!
                    if (namedParamPos >= parameterPosition)
                        moveToNextParameterPosition();

                    continue;
                }

                // for parameters that are NOT named - we will also do a type check to see whether the parameter is a good match or not
                var tmpParameterPosition = parameterPosition;
                var foundMatchAhead = false;

                while (true)
                {
                    // we can't scan past the parameters for the arg
                    if (parameterPosition >= parameterCount) break;

                    var conversion = CheckArgumentForApplicability(symbol, argumentPosition, parameterPosition, arguments, parameters, ignoreOpenTypes: true);
                    if (!conversion.Exists)
                    {
                        // the given argument doesn't match the expected type for the parameter...

                        // if the parameter isn't optional... we can't really do anything about it... so stop...
                        if (!CanBeOptional(parameters[parameterPosition], isMethodGroupConversion)) break;

                        // it's not a match... we can possibly try to "scan ahead" and see if there is any other... just skip this parameter position... and try with the next...
                        parameterPosition += 1;
                        continue;
                    }
                    else
                    {
                        foundMatchAhead = true;
                        break;
                    }
                }

                // restore the state if failed to scan ahead
                if (!foundMatchAhead)
                {
                    parameterPosition = tmpParameterPosition;

                    // set this as the first unmatched argument... since we didn't have any match
                    if (unmatchedArgumentIndex == null)
                    {
                        unmatchedArgumentIndex = argumentPosition;
                        unmatchedArgumentIsNamed = false;
                    }
                }
                else
                {
                    // map the parameter
                    mapParameter(argumentPosition, parameterPosition);
                }

                // increment the expected parameter position
                moveToNextParameterPosition();
            }

            ParameterMap argsToParameters = new ParameterMap(parametersPositions, argumentCount);

            // We have analyzed every argument and tried to make it correspond to a particular parameter. 
            // We must now answer the following questions:
            //
            // (1) Is there any argument without a corresponding parameter?
            // (2) Was there any named argument that specified a parameter that was already
            //     supplied with a positional parameter?
            // (3) Is there any non-optional parameter without a corresponding argument?
            // (4) Is there any named argument that were specified twice?
            //
            // If the answer to any of these questions is "yes" then the method is not applicable.
            // It is possible that the answer to any number of these questions is "yes", and so
            // we must decide which error condition to prioritize when reporting the error, 
            // should we need to report why a given method is not applicable. We prioritize
            // them in the given order.

            // (1) Is there any argument without a corresponding parameter?

            if (unmatchedArgumentIndex != null)
            {
                if (unmatchedArgumentIsNamed.Value)
                {
                    return ArgumentAnalysisResult.NoCorrespondingNamedParameter(unmatchedArgumentIndex.Value);
                }
                else
                {
                    return ArgumentAnalysisResult.NoCorrespondingParameter(unmatchedArgumentIndex.Value);
                }
            }

            // (2) was there any named argument that specified a parameter that was already
            //     supplied with a positional parameter?

            int? nameUsedForPositional = NameUsedForPositional(arguments, argsToParameters);
            if (nameUsedForPositional != null)
            {
                return ArgumentAnalysisResult.NameUsedForPositional(nameUsedForPositional.Value);
            }

            // (3) Is there any non-optional parameter without a corresponding argument?

            int? requiredParameterMissing = CheckForMissingRequiredParameter(argsToParameters, parameters, isMethodGroupConversion, expanded);
            if (requiredParameterMissing != null)
            {
                return ArgumentAnalysisResult.RequiredParameterMissing(requiredParameterMissing.Value);
            }

            // __arglist cannot be used with named arguments (as it doesn't have a name)
            if (arguments.Names.Any() && arguments.Names.Last() != null && isVararg)
            {
                return ArgumentAnalysisResult.RequiredParameterMissing(parameters.Length);
            }

            // (4) Is there any named argument that were specified twice?

            int? duplicateNamedArgument = CheckForDuplicateNamedArgument(arguments);
            if (duplicateNamedArgument != null)
            {
                return ArgumentAnalysisResult.DuplicateNamedArgument(duplicateNamedArgument.Value);
            }

            // We're good; this one might be applicable in the given form.

            var result = expanded ?
                ArgumentAnalysisResult.ExpandedForm(argsToParameters.ToImmutableArray()) :
                ArgumentAnalysisResult.NormalForm(argsToParameters.ToImmutableArray());

            result.HasSpreadParameters = hasAnyMatchedSpreadArgs;

            return result;
        }

        private Conversion CheckArgumentForApplicability(
            Symbol candidate,
            int argPosition,
            int paramPosition,
            AnalyzedArguments arguments,
            ImmutableArray<ParameterSymbol> parameters,
            bool ignoreOpenTypes = true
            )
        {
            var argument = arguments.Arguments[argPosition];
            var param = parameters[paramPosition];
            var parameterType = param.Type;

            // we need 
            RefKind argRefKind = arguments.RefKind(argPosition);
            RefKind parRefKind = param.RefKind;
            bool forExtensionMethodThisArg = arguments.IsExtensionMethodThisArgument(argPosition);

            if (forExtensionMethodThisArg)
            {
                Debug.Assert(argRefKind == RefKind.None);
                if (parRefKind == RefKind.Ref)
                {
                    // For ref extension methods, we omit the "ref" modifier on the receiver arguments
                    // Passing the parameter RefKind for finding the correct conversion.
                    // For ref-readonly extension methods, argumentRefKind is always None.
                    argRefKind = parRefKind;
                }
            }

            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            return CheckArgumentForApplicability(
                candidate,
                argument,
                argRefKind,
                parameterType,
                parRefKind,
                ignoreOpenTypes,
                ref useSiteDiagnostics,
                forExtensionMethodThisArg
            );
        }

        private static int? CheckForBadNonTrailingNamedArgument(AnalyzedArguments arguments, ParameterMap argsToParameters, ImmutableArray<ParameterSymbol> parameters)
        {
            // Is there any named argument used out-of-position and followed by unnamed arguments?

            // If the map is trivial then clearly not.
            if (argsToParameters.IsTrivial)
            {
                return null;
            }

            // Find the first named argument which is used out-of-position
            int foundPosition = -1;
            int length = arguments.Arguments.Count;
            for (int i = 0; i < length; i++)
            {
                int parameter = argsToParameters[i];
                if (parameter != -1 && parameter != i && arguments.Name(i) != null)
                {
                    foundPosition = i;
                    break;
                }
            }

            if (foundPosition != -1)
            {
                // Verify that all the following arguments are named
                for (int i = foundPosition + 1; i < length; i++)
                {
                    if (arguments.Name(i) == null)
                    {
                        return foundPosition;
                    }
                }
            }

            return null;
        }

        private static int? TryGetNamedParameterPosition(
            ImmutableArray<ParameterSymbol> memberParameters,
            AnalyzedArguments arguments,
            int argumentPosition,
            out bool isNamed,
            out bool isSpread
            )
        {
            isSpread = false;
            isNamed = arguments.Names.Count > argumentPosition && arguments.Names[argumentPosition] != null;
            if (!isNamed) return null;

            // find the matching named parameter
            var name = arguments.Names[argumentPosition].Identifier.ValueText;
            for (int p = 0; p < memberParameters.Length; ++p)
            {
                if (memberParameters[p].Name == name)
                {
                    return p;
                }
            }

            // if now matching named parameter, we can try with the spread parameters
            for (int p = 0; p < memberParameters.Length; ++p)
            {
                var param = memberParameters[p];
                if (!param.IsSpread) continue;

                // match the parameter to the spread
                var spreadMember = param.Type?.GetMembers().FirstOrDefault(m => m.Name == name);
                if (spreadMember != null)
                {
                    isSpread = true;
                    return p;
                }
            }

            return null;
        }

        private static int? CorrespondsToAnyParameter(
            ImmutableArray<ParameterSymbol> memberParameters,
            bool expanded,
            AnalyzedArguments arguments,
            int argumentPosition,
            int nextParameterPosition,
            bool isValidParams,
            bool isVararg,
            out bool isNamedArgument,
            ref bool seenNamedParams,
            ref bool seenOutOfPositionNamedArgument)
        {
            // Spec 7.5.1.1: Corresponding parameters:
            // For each argument in an argument list there has to be a corresponding parameter in
            // the function member or delegate being invoked. The parameter list used in the
            // following is determined as follows:
            // - For virtual methods and indexers defined in classes, the parameter list is picked from the most specific 
            //   declaration or override of the function member, starting with the static type of the receiver, and searching through its base classes.
            // - For interface methods and indexers, the parameter list is picked form the most specific definition of the member, 
            //   starting with the interface type and searching through the base interfaces. If no unique parameter list is found, 
            //   a parameter list with inaccessible names and no optional parameters is constructed, so that invocations cannot use 
            //   named parameters or omit optional arguments.
            // - For partial methods, the parameter list of the defining partial method declaration is used.
            // - For all other function members and delegates there is only a single parameter list, which is the one used.
            //
            // The position of an argument or parameter is defined as the number of arguments or
            // parameters preceding it in the argument list or parameter list.
            //
            // The corresponding parameters for function member arguments are established as follows:
            // 
            // Arguments in the argument-list of instance constructors, methods, indexers and delegates:

            isNamedArgument = arguments.Names.Count > argumentPosition && arguments.Names[argumentPosition] != null;

            if (!isNamedArgument)
            {
                // Spec:
                // - A positional argument where a fixed parameter occurs at the same position in the
                //   parameter list corresponds to that parameter.
                // - A positional argument of a function member with a parameter array invoked in its
                //   normal form corresponds to the parameter array, which must occur at the same
                //   position in the parameter list.
                // - A positional argument of a function member with a parameter array invoked in its
                //   expanded form, where no fixed parameter occurs at the same position in the
                //   parameter list, corresponds to an element in the parameter array.

                if (seenNamedParams)
                {
                    // Unnamed arguments after a named argument corresponding to a params parameter cannot correspond to any parameters
                    return null;
                }

                if (seenOutOfPositionNamedArgument)
                {
                    // Unnamed arguments after an out-of-position named argument cannot correspond to any parameters
                    return null;
                }

                int parameterCount = memberParameters.Length + (isVararg ? 1 : 0);
                if (nextParameterPosition >= parameterCount)
                {
                    return expanded ? parameterCount - 1 : (int?)null;
                }

                return nextParameterPosition;
            }
            else
            {
                // SPEC: A named argument corresponds to the parameter of the same name in the parameter list. 

                // SPEC VIOLATION: The intention of this line of the specification, when contrasted with
                // SPEC VIOLATION: the lines on positional arguments quoted above, was to disallow a named
                // SPEC VIOLATION: argument from corresponding to an element of a parameter array when 
                // SPEC VIOLATION: the method was invoked in its expanded form. That is to say that in
                // SPEC VIOLATION: this case:  M(params int[] x) ... M(x : 1234); the named argument 
                // SPEC VIOLATION: corresponds to x in the normal form (and is then inapplicable), but
                // SPEC VIOLATION: the named argument does *not* correspond to a member of params array
                // SPEC VIOLATION: x in the expanded form.
                // SPEC VIOLATION: Sadly that is not what we implemented in C# 4, and not what we are 
                // SPEC VIOLATION: implementing here. If you do that, we make x correspond to the 
                // SPEC VIOLATION: parameter array and allow the candidate to be applicable in its
                // SPEC VIOLATION: expanded form.

                var name = arguments.Names[argumentPosition];
                for (int p = 0; p < memberParameters.Length; ++p)
                {
                    // p is initialized to zero; it is ok for a named argument to "correspond" to
                    // _any_ parameter (not just the parameters past the point of positional arguments)
                    if (memberParameters[p].Name == name.Identifier.ValueText)
                    {
                        if (isValidParams && p == memberParameters.Length - 1)
                        {
                            seenNamedParams = true;
                        }

                        if (p != argumentPosition)
                        {
                            seenOutOfPositionNamedArgument = true;
                        }

                        return p;
                    }
                }
            }

            return null;
        }

        private static ArgumentAnalysisResult AnalyzeArgumentsForNormalFormNoNamedArguments(
            ImmutableArray<ParameterSymbol> parameters,
            AnalyzedArguments arguments,
            bool isMethodGroupConversion,
            bool isVararg)
        {
            Debug.Assert(!parameters.IsDefault);
            Debug.Assert(arguments != null);
            Debug.Assert(arguments.Names.Count == 0);

            // We simulate an additional non-optional parameter for a vararg method.
            int parameterCount = parameters.Length + (isVararg ? 1 : 0);
            int argumentCount = arguments.Arguments.Count;

            // If there are no named arguments then analyzing the argument and parameter
            // matching in normal form is simple: each argument corresponds exactly to
            // the matching parameter, and if there are not enough arguments then the
            // unmatched parameters had better all be optional. If there are too 
            // few parameters then one of the arguments has no matching parameter. 
            // Otherwise, everything is just right.

            if (argumentCount < parameterCount)
            {
                for (int parameterPosition = argumentCount; parameterPosition < parameterCount; ++parameterPosition)
                {
                    if (parameters.Length == parameterPosition || !CanBeOptional(parameters[parameterPosition], isMethodGroupConversion))
                    {
                        return ArgumentAnalysisResult.RequiredParameterMissing(parameterPosition);
                    }
                }
            }
            else if (parameterCount < argumentCount)
            {
                return ArgumentAnalysisResult.NoCorrespondingParameter(parameterCount);
            }

            // A null map means that every argument in the argument list corresponds exactly to
            // the same position in the formal parameter list.
            return ArgumentAnalysisResult.NormalForm(default(ImmutableArray<int>));
        }

        private static bool CanBeOptional(ParameterSymbol parameter, bool isMethodGroupConversion)
        {
            // NOTE: Section 6.6 will be slightly updated:
            //
            //   - The candidate methods considered are only those methods that are applicable in their
            //     normal form (§7.5.3.1), and do not omit any optional parameters. Thus, candidate methods
            //     are ignored if they are applicable only in their expanded form, or if one or more of their
            //     optional parameters do not have a corresponding parameter in the targeted delegate type.
            //   
            // Therefore, no parameters are optional when performing method group conversion.  Alternatively,
            // we could eliminate methods based on the number of arguments, but then we wouldn't be able to
            // fall back on them if no other candidates were available.

            return !isMethodGroupConversion && parameter.IsOptional;
        }

        private static int? NameUsedForPositional(AnalyzedArguments arguments, ParameterMap argsToParameters)
        {
            // Was there a named argument used for a previously-supplied positional argument?

            // If the map is trivial then clearly not. 
            if (argsToParameters.IsTrivial)
            {
                return null;
            }

            // PERFORMANCE: This is an O(n-squared) algorithm, but n will typically be small.  We could rewrite this
            // PERFORMANCE: as a linear algorithm if we wanted to allocate more memory.

            for (int argumentPosition = 0; argumentPosition < argsToParameters.Length; ++argumentPosition)
            {
                if (arguments.Name(argumentPosition) != null)
                {
                    for (int i = 0; i < argumentPosition; ++i)
                    {
                        if (arguments.Name(i) == null)
                        {
                            if (argsToParameters[argumentPosition] == argsToParameters[i])
                            {
                                // Error; we've got a named argument that corresponds to 
                                // a previously-given positional argument.
                                return argumentPosition;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static int? CheckForMissingRequiredParameter(
            ParameterMap argsToParameters,
            ImmutableArray<ParameterSymbol> parameters,
            bool isMethodGroupConversion,
            bool expanded)
        {
            Debug.Assert(!(expanded && isMethodGroupConversion));

            // If we're in the expanded form then the final parameter is always optional,
            // so we'll just skip it entirely.

            int count = expanded ? parameters.Length - 1 : parameters.Length;

            // We'll take an early out here. If the map from arguments to parameters is trivial
            // and there are as many arguments as parameters in that map, then clearly no 
            // required parameter is missing.

            if (argsToParameters.IsTrivial && count <= argsToParameters.Length)
            {
                return null;
            }

            // This is an O(n squared) algorithm, but (1) we avoid allocating any more heap memory, and
            // (2) n is likely to be small, both because the number of parameters in a method is typically
            // small, and because methods with many parameters make most of them optional. We could make
            // this linear easily enough if we needed to but we'd have to allocate more heap memory and
            // we'd rather not pressure the garbage collector.

            for (int p = 0; p < count; ++p)
            {
                if (CanBeOptional(parameters[p], isMethodGroupConversion))
                {
                    continue;
                }

                bool found = false;
                for (int arg = 0; arg < argsToParameters.Length; ++arg)
                {
                    found = (argsToParameters[arg] == p);
                    if (found)
                    {
                        break;
                    }
                }
                if (!found)
                {
                    return p;
                }
            }

            return null;
        }

        private static int? CheckForDuplicateNamedArgument(AnalyzedArguments arguments)
        {
            if (arguments.Names.IsEmpty())
            {
                // No checks if there are no named arguments
                return null;
            }

            var alreadyDefined = PooledHashSet<string>.GetInstance();
            for (int i = 0; i < arguments.Names.Count; ++i)
            {
                string name = arguments.Name(i);

                if (name is null)
                {
                    // Skip unnamed arguments
                    continue;
                }

                if (!alreadyDefined.Add(name))
                {
                    alreadyDefined.Free();
                    return i;
                }
            }

            alreadyDefined.Free();
            return null;
        }
    }
}
