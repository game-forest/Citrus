setlocal EnableDelayedExpansion

for %%t in (armeabi-v7a arm64-v8a x86 x86_64) do (
	set BUILD_DIR=build/android_%%t
	cmake -B !BUILD_DIR! -G "Unix Makefiles" -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=%ANDROID_NDK_ROOT%/build/cmake/android.toolchain.cmake -DANDROID_STL=c++_static -DANDROID_ABI=%%t -DCMAKE_MAKE_PROGRAM=%ANDROID_NDK_ROOT%\prebuilt\windows-x86_64\bin\make -DCMAKE_INSTALL_PREFIX=build/install/%%t -DSHADER_COMPILER_SHARED_LIB=ON
	cmake --build !BUILD_DIR! --config Release --target install
)

endlocal