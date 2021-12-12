@echo on
rd /q /s bin
mkdir bin
copy ..\Ra2CsfToolsGUI\bin\Release\*.exe bin\
copy ..\Ra2CsfToolsGUI\bin\Release\*.exe.config bin\
copy ..\Ra2CsfToolsGUI\bin\Release\*.dll bin\
pause