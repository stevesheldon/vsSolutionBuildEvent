.nuget\nuget restore vsSolutionBuildEvent_net40.sln 
"C:\Program Files (x86)\MSBuild\12.0\bin\msbuild.exe" "vsSolutionBuildEvent_net40.sln" /verbosity:detailed  /l:"packages\vsSBE.CI.MSBuild.1.0.4\bin\CI.MSBuild.dll" /m /t:Rebuild /p:Configuration=CI_Debug /p:Platform="Any CPU"