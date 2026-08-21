# GameHost —— CEF 87 独立游戏宿主，每账号一个进程。
# 架构按编译器位数自动选择（win32 / x64）：
#   以 vcvarsall.bat <arch> 进入 MSVC 环境后：
#   cmake -S app -B build/app -G Ninja
#   cmake --build build/app
#
# 注意：PPAPI Flash 插件只有 32 位（Adobe 从未发布 64 位 PPAPI Flash），
#       因此 x64 构建仅能在编译层面产出 exe，无法加载 Flash，游戏不可运行。
#
# 依赖：CEF 87 SDK、Flash.cn PPAPI 34.0.0.380（tools/download_deps.ps1 下载至 third_party/）

cmake_minimum_required(VERSION 3.16)
project(huoyin_launcher VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# third_party 目录（相对本文件上两级）
get_filename_component(NARUTO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/.." ABSOLUTE)
set(CEF_SDK_DIR "${NARUTO_ROOT}/third_party/cef_sdk/CEF")

# 按编译器位数选择库目录（CEF SDK 内部分为 win32/ 与 x64/）与运行时目录
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(CEF_PLATFORM_DIR "x64")
    set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime_x64")
else()
    set(CEF_PLATFORM_DIR "win32")
    set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime")
endif()

if(NOT EXISTS "${CEF_SDK_DIR}/include/cef_version.h")
    message(FATAL_ERROR "未找到 CEF SDK。请先运行: powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1")
endif()

add_executable(huoyin_launcher WIN32
    src/main.cpp
    src/app_log.cpp
    src/app_log.h
    src/frameless_window.cpp
    src/frameless_window.h
    src/no_console_hook.cpp
    src/no_console_hook.h
)

target_include_directories(huoyin_launcher PRIVATE
    "${CEF_SDK_DIR}"
)

target_link_libraries(huoyin_launcher PRIVATE
    "${CEF_SDK_DIR}/${CEF_PLATFORM_DIR}/release/libcef.lib"
    "${CEF_SDK_DIR}/${CEF_PLATFORM_DIR}/release/VS2017/libcef_dll_wrapper.lib"
    ws2_32
    dbghelp
    version
    winmm
    imm32
)

target_compile_definitions(huoyin_launcher PRIVATE
    _CRT_SECURE_NO_WARNINGS
    NOMINMAX
    UNICODE
    _UNICODE
)

# 源码含中文注释，强制 MSVC 以 UTF-8 解析源码
if(MSVC)
    target_compile_options(huoyin_launcher PRIVATE /utf-8)
endif()

# 构建后复制 CEF 运行时、Flash 插件到可执行目录
# （Flash 插件 pepflashplayer.dll 仅有 32 位，x64 构建跳过复制）
if(CEF_PLATFORM_DIR STREQUAL "win32")
    add_custom_command(TARGET huoyin_launcher POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${CEF_RUNTIME_DIR}" "$<TARGET_FILE_DIR:huoyin_launcher>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${NARUTO_ROOT}/third_party/pepflashplayer.dll"
                "$<TARGET_FILE_DIR:huoyin_launcher>/pepflashplayer.dll"
        COMMENT "复制 CEF 运行时与 Flash 插件到可执行目录"
    )
else()
    add_custom_command(TARGET huoyin_launcher POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${CEF_RUNTIME_DIR}" "$<TARGET_FILE_DIR:huoyin_launcher>"
        COMMENT "复制 CEF 运行时到可执行目录（x64 构建无 Flash 插件）"
    )
endif()
