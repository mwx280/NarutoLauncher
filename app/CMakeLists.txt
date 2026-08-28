# CEFFlashGameHost（火影版）—— CEF 87 独立游戏宿主，每账号一个进程。
# 架构按编译器位数自动选择（win32 / x64）：
#   以 vcvarsall.bat <arch> 进入 MSVC 环境后：
#   cmake -S app -B build/app -G Ninja
#   cmake --build build/app
#
# 依赖：CEF 87 SDK、MinHook（third_party/minhook，随仓库分发）、
#       Flash 插件（官方 32/64 位版本，随仓库分发）

cmake_minimum_required(VERSION 3.16)
project(cef_flash_game_host VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# third_party 目录（相对本文件上两级）
get_filename_component(NARUTO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/.." ABSOLUTE)
set(CEF_SDK_DIR "${NARUTO_ROOT}/third_party/cef_sdk/CEF")
set(MINHOOK_DIR "${NARUTO_ROOT}/third_party/minhook")

# 按编译器位数选择库目录（CEF SDK 内部分为 win32/ 与 x64/）与运行时目录
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(CEF_PLATFORM_DIR "x64")
    set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime_x64")
    set(MINHOOK_LIB "${MINHOOK_DIR}/lib/MinHook.x64.lib")
else()
    set(CEF_PLATFORM_DIR "win32")
    set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime")
    set(MINHOOK_LIB "${MINHOOK_DIR}/lib/MinHook.x86.lib")
endif()

if(NOT EXISTS "${CEF_SDK_DIR}/include/cef_version.h")
    message(FATAL_ERROR "未找到 CEF SDK。请先运行: powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1")
endif()

add_executable(cef_flash_game_host WIN32
    src/main.cpp
    src/app_log.cpp
    src/app_log.h
    src/frameless_window.cpp
    src/frameless_window.h
    src/no_console_hook.cpp
    src/no_console_hook.h
    src/flash_hook.cpp
    src/flash_hook.h
    src/speed_hook.cpp
    src/speed_hook.h
    src/globals.h
    src/params.cpp
    src/params.h
    src/host_app.cpp
    src/host_app.h
    src/host_client.cpp
    src/host_client.h
)

target_include_directories(cef_flash_game_host PRIVATE
    "${CEF_SDK_DIR}"
    "${MINHOOK_DIR}/include"
)

target_link_libraries(cef_flash_game_host PRIVATE
    "${CEF_SDK_DIR}/${CEF_PLATFORM_DIR}/release/libcef.lib"
    "${CEF_SDK_DIR}/${CEF_PLATFORM_DIR}/release/VS2017/libcef_dll_wrapper.lib"
    "${MINHOOK_LIB}"
    ws2_32
    dbghelp
    version
    winmm
    imm32
)

target_compile_definitions(cef_flash_game_host PRIVATE
    _CRT_SECURE_NO_WARNINGS
    NOMINMAX
    UNICODE
    _UNICODE
)

# 源码含中文注释，强制 MSVC 以 UTF-8 解析源码
if(MSVC)
    target_compile_options(cef_flash_game_host PRIVATE /utf-8)
endif()

# 反破解硬化：Release 全程序优化 + 链接时代码生成（/GL /LTCG），
# 提高优化程度并增加反汇编还原难度；不生成 PDB（CMake Release 默认）。
set_target_properties(cef_flash_game_host PROPERTIES
    COMPILE_FLAGS_RELEASE "/GL /O2"
    LINK_FLAGS_RELEASE "/LTCG"
)

# 构建后复制 CEF 运行时、Flash 插件、MinHook 运行时到可执行目录
# （Flash 插件官方同时提供 32 位与 64 位版本：pepflashplayer.dll / pepflashplayer_x64.dll，
#   均复制为同目录下的 pepflashplayer.dll，与宿主硬编码的插件名保持一致）
if(CEF_PLATFORM_DIR STREQUAL "win32")
    set(FLASH_SRC "${NARUTO_ROOT}/third_party/pepflashplayer.dll")
    set(MINHOOK_RUNTIME "${MINHOOK_DIR}/lib/MinHook.x86.dll")
else()
    set(FLASH_SRC "${NARUTO_ROOT}/third_party/pepflashplayer_x64.dll")
    set(MINHOOK_RUNTIME "${MINHOOK_DIR}/lib/MinHook.x64.dll")
endif()
add_custom_command(TARGET cef_flash_game_host POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CEF_RUNTIME_DIR}" "$<TARGET_FILE_DIR:cef_flash_game_host>"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${FLASH_SRC}"
            "$<TARGET_FILE_DIR:cef_flash_game_host>/pepflashplayer.dll"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${MINHOOK_RUNTIME}"
            "$<TARGET_FILE_DIR:cef_flash_game_host>"
    COMMENT "复制 CEF 运行时、Flash 插件与 MinHook 运行时到可执行目录"
)
