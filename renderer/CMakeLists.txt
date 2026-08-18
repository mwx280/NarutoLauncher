# 渲染器（Renderer）—— C++ · 目标架构 x86
# 配置方式（x86 目标，MSVC 交叉环境）：
#   以 vcvarsarm64_x86.bat / vcvars32.bat / vcvarsamd64_x86.bat 进入 MSVC 环境后：
#   cmake -S renderer -B build/renderer -G Ninja
#   cmake --build build/renderer
#
# 注意：渲染器固定 x86（与 Flash PPAPI x86 插件架构一致，三平台通用）。
# CEF 87.1.13 依赖由 tools/download_deps.ps1 下载到 third_party/（NuGet cef.sdk + cef.redist.x86）。

cmake_minimum_required(VERSION 3.16)
project(naruto_renderer VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(naruto_renderer WIN32
    src/main.cpp
)

# third_party 目录（相对本文件上两级）
get_filename_component(NARUTO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/.." ABSOLUTE)
set(CEF_SDK_DIR "${NARUTO_ROOT}/third_party/cef_sdk/CEF")
set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime")

if(NOT EXISTS "${CEF_SDK_DIR}/include/cef_version.h")
    message(FATAL_ERROR "未找到 CEF SDK。请先运行: powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1")
endif()

# CEF 头文件
target_include_directories(naruto_renderer PRIVATE
    "${CEF_SDK_DIR}/include"
    "${CEF_SDK_DIR}/include/capi"
    "${CEF_SDK_DIR}/include/internal"
)

# 链接 CEF（x86 / release）
target_link_libraries(naruto_renderer PRIVATE
    "${CEF_SDK_DIR}/win32/release/libcef.lib"
    "${CEF_SDK_DIR}/win32/release/VS2017/libcef_dll_wrapper.lib"
)

target_compile_definitions(naruto_renderer PRIVATE
    _CRT_SECURE_NO_WARNINGS
    NOMINMAX
)

# 源码含中文注释，强制 MSVC 以 UTF-8 解析源码
if(MSVC)
    target_compile_options(naruto_renderer PRIVATE /utf-8)
endif()

# 阶段 1/2 待接入：
# - CEF 宿主（browser + subprocess 双模式）
# - Flash PPAPI 注册（--ppapi-flash-path / --ppapi-flash-version）
# - cookie store 持久化、登录/选区/游戏加载
# - Named Pipe IPC 服务端
# - CefMessageRouter JS 桥、变速 hook 模块
