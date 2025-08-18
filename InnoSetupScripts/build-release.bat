@echo on

rd /q /s ..\Ra2CsfToolsGUI\bin\Release\net48\
dotnet build -c Release ..\Ra2CsfToolsGUI\Ra2CsfToolsGUI.csproj

rd /q /s bin
mkdir bin

copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.exe bin\
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.exe.config bin\
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.dll bin\
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.pdb bin\
mkdir bin\zh-Hans
copy ..\Ra2CsfToolsGUI\bin\Release\net48\zh-Hans\*.dll bin\zh-Hans\
pause