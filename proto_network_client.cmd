SET WORK_DIR=.\proto_conv\
SET MYTH_DIR=..\..\server\server\
SET PROTOC=..\ProtoGen\protoc.exe
cd %WORK_DIR%
%PROTOC% --proto_path=. --cpp_out=. shared.proto
xcopy .\shared.pb.* %MYTH_DIR%\config\data /Y /Q
cd ..\

REM client: convert network protoFiles to coreCompile_dll and preCompile_dll
pushd ..\protocol
SET CSC20=c:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe
SET PROTOGEN=..\tools\ProtoGen\protogen.exe
FOR %%P IN (*.proto) DO %PROTOGEN% -i:%%P -o:..\tools\mw-proto\%%~nP.cs -ns:mw
popd
SET CLIENT=..\client
%CSC20% /target:library /out:mw-proto\mw-proto.dll /reference:%CLIENT%\Assets\Libs\protobuf-net.dll /debug- /optimize+ mw-proto\*.cs
mw-serializer-builder\mw-serializer-builder.exe %CD%\mw-proto\mw-proto.dll mw-proto-serializer 50
xcopy mw-proto\mw-proto.dll ..\client\Assets\Libs\ /Y /Q
xcopy mw-proto-serializer*.dll ..\client\Assets\Libs\ /Y /Q
del /a /f mw-proto-serializer*.dll
REM sever: convert network protoFiles to pythonFiles
pushd ..\protocol
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\login.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\qgame.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\pkgid.proto
pause