@echo off
call BumpVersion.cmd || exit
call Package.cmd || exit
if "%1" == "publish" (
    goto publish
) else if "%1" == "-publish" (
    goto publish
) else (
    goto end
)
:publish
call Publish.cmd
:end