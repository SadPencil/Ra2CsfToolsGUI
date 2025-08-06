@echo on
rd /q /s bin
mkdir bin
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.exe bin\
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.exe.config bin\
copy ..\Ra2CsfToolsGUI\bin\Release\net48\*.dll bin\
pause