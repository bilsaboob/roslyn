// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal struct ArgumentAnalysisResult
    {
        public readonly ImmutableArray<int> ArgsToParamsOpt;
        public readonly ImmutableArray<int> UnmatchedArgsToParamsOpt;
        public readonly int ArgumentPosition;
        public readonly int ParameterPosition;
        public readonly ArgumentAnalysisResultKind Kind;

        public int ArgumentFromParameter(int param)
        {
            Debug.Assert(param >= 0);
            if (ArgsToParamsOpt.IsDefault) return -1;
            for (var i = 0; i < ArgsToParamsOpt.Length; ++i)
            {
                if (ArgsToParamsOpt[i] == param) return i;
            }
            return -1;
        }

        public int UnmatchedArgumentFromParameter(int param)
        {
            Debug.Assert(param >= 0);
            if (UnmatchedArgsToParamsOpt.IsDefault) return -1;
            for (var i = 0; i < UnmatchedArgsToParamsOpt.Length; ++i)
            {
                if (UnmatchedArgsToParamsOpt[i] == param) return i;
            }
            return -1;
        }

        public int ParameterFromArgument(int arg)
        {
            Debug.Assert(arg >= 0);
            if (ArgsToParamsOpt.IsDefault)
            {
                return arg;
            }
            Debug.Assert(arg < ArgsToParamsOpt.Length);
            return ArgsToParamsOpt[arg];
        }

        public int ParameterFromUnmatchedArgument(int arg)
        {
            Debug.Assert(arg >= 0);
            if (UnmatchedArgsToParamsOpt.IsDefault)
            {
                return arg;
            }
            Debug.Assert(arg < UnmatchedArgsToParamsOpt.Length);
            return UnmatchedArgsToParamsOpt[arg];
        }

        private ArgumentAnalysisResult(ArgumentAnalysisResultKind kind,
                                    int argumentPosition,
                                    int parameterPosition,
                                    ImmutableArray<int> argsToParamsOpt,
                                    ImmutableArray<int> unmatchedArgsToParamsOpt = default)
        {
            this.Kind = kind;
            this.ArgumentPosition = argumentPosition;
            this.ParameterPosition = parameterPosition;
            this.ArgsToParamsOpt = argsToParamsOpt;
            this.UnmatchedArgsToParamsOpt = unmatchedArgsToParamsOpt;
            this.HasSpreadParameters = false;
            this.HasUnmatchedLambdaArguments = false;
            this.HasLambdaArgumentsWithThisScope = false;
        }

        public bool HasSpreadParameters { get; set; }

        public bool HasUnmatchedLambdaArguments { get; set; }

        public bool HasLambdaArgumentsWithThisScope { get; set; }

        public bool IsValid
        {
            get
            {
                return this.Kind < ArgumentAnalysisResultKind.FirstInvalid;
            }
        }

        public static ArgumentAnalysisResult NameUsedForPositional(int argumentPosition)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.NameUsedForPositional, argumentPosition, 0, default(ImmutableArray<int>));
        }

        public static ArgumentAnalysisResult NoCorrespondingParameter(int argumentPosition, ImmutableArray<int> argsToParamOpt = default(ImmutableArray<int>), ImmutableArray<int> unmatchedArgsToParamOpt = default(ImmutableArray<int>))
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.NoCorrespondingParameter, argumentPosition, 0, argsToParamOpt, unmatchedArgsToParamOpt);
        }

        public static ArgumentAnalysisResult NoCorrespondingNamedParameter(int argumentPosition, ImmutableArray<int> argsToParamOpt = default(ImmutableArray<int>), ImmutableArray<int> unmatchedArgsToParamOpt = default(ImmutableArray<int>))
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.NoCorrespondingNamedParameter, argumentPosition, 0, argsToParamOpt, unmatchedArgsToParamOpt);
        }

        public static ArgumentAnalysisResult DuplicateNamedArgument(int argumentPosition)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.DuplicateNamedArgument, argumentPosition, 0, default(ImmutableArray<int>));
        }

        public static ArgumentAnalysisResult RequiredParameterMissing(int parameterPosition)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.RequiredParameterMissing, 0, parameterPosition, default(ImmutableArray<int>));
        }

        public static ArgumentAnalysisResult BadNonTrailingNamedArgument(int argumentPosition)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.BadNonTrailingNamedArgument, argumentPosition, 0, default(ImmutableArray<int>));
        }

        public static ArgumentAnalysisResult NormalForm(ImmutableArray<int> argsToParamsOpt)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.Normal, 0, 0, argsToParamsOpt);
        }

        public static ArgumentAnalysisResult ExpandedForm(ImmutableArray<int> argsToParamsOpt)
        {
            return new ArgumentAnalysisResult(ArgumentAnalysisResultKind.Expanded, 0, 0, argsToParamsOpt);
        }

#if DEBUG        
        private string Dump()
        {
            string s = "";
            switch (Kind)
            {
                case ArgumentAnalysisResultKind.Normal:
                    s += "Valid in normal form.";
                    break;
                case ArgumentAnalysisResultKind.Expanded:
                    s += "Valid in expanded form.";
                    break;
                case ArgumentAnalysisResultKind.NameUsedForPositional:
                    s += "Invalid because argument " + ArgumentPosition + " had a name.";
                    break;
                case ArgumentAnalysisResultKind.DuplicateNamedArgument:
                    s += "Invalid because named argument " + ArgumentPosition + " was specified twice.";
                    break;
                case ArgumentAnalysisResultKind.NoCorrespondingParameter:
                    s += "Invalid because argument " + ArgumentPosition + " has no corresponding parameter.";
                    break;
                case ArgumentAnalysisResultKind.NoCorrespondingNamedParameter:
                    s += "Invalid because named argument " + ArgumentPosition + " has no corresponding parameter.";
                    break;
                case ArgumentAnalysisResultKind.RequiredParameterMissing:
                    s += "Invalid because parameter " + ParameterPosition + " has no corresponding argument.";
                    break;
                case ArgumentAnalysisResultKind.BadNonTrailingNamedArgument:
                    s += "Invalid because named argument " + ParameterPosition + " is used out of position but some following argument(s) are not named.";
                    break;
            }

            if (!ArgsToParamsOpt.IsDefault)
            {
                for (int i = 0; i < ArgsToParamsOpt.Length; ++i)
                {
                    s += "\nArgument " + i + " corresponds to parameter " + ArgsToParamsOpt[i];
                }
            }

            return s;
        }
#endif

    }
}
