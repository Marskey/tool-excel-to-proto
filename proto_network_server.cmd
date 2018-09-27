SET WORK_DIR=.\proto_conv\
SET MYTH_DIR=..\..\server\server\
SET PROTOC=..\ProtoGen\protoc.exe
cd %WORK_DIR%
%PROTOC% --proto_path=. --cpp_out=. shared.proto
xcopy .\shared.pb.* %MYTH_DIR%\config\data /Y /Q
xcopy .\shared.pb.* ..\code /Y /Q
cd ..\

REM sever: convert network protoFiles to pythonFiles
pushd ..\protocol
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\login.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\qgame.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\server\proto\ .\pkgid.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\svr_proto\ .\auth.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\svr_proto\ .\log.proto
..\tools\ProtoGen\protoc.exe --proto_path=. --cpp_out=..\server\svr_proto\ .\svrpkgid.proto
pause