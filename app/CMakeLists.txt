# 宿主（app）—— CEF 87 x86 单宿主：UI(HTML/Vue) + 游戏(Flash) 同进程
# 配置方式（x86 目标，MSVC 交叉环境）：
#   以 vcvarsarm64_x86.bat / vcvars32.bat / vcvarsamd64_x86.bat 进入 MSVC 环境后：
#   cmake -S app -B build/app -G Ninja
#   cmake --build build/app
#
# 依赖：CEF 87 SDK（tools/download_deps.ps1 下载到 third_party/）
#       Vue UI（先 cd app/ui && npm install && npm run build，产物在 app/ui/dist）

cmake_minimum_required(VERSION 3.16)
project(huoyin_launcher VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# third_party 目录（相对本文件上两级）
get_filename_component(NARUTO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/.." ABSOLUTE)
set(CEF_SDK_DIR "${NARUTO_ROOT}/third_party/cef_sdk/CEF")
set(CEF_RUNTIME_DIR "${NARUTO_ROOT}/third_party/cef_runtime")

if(NOT EXISTS "${CEF_SDK_DIR}/include/cef_version.h")
    message(FATAL_ERROR "未找到 CEF SDK。请先运行: powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1")
endif()

add_executable(huoyin_launcher WIN32
    src/main.cpp
    src/app_log.cpp
    src/app_log.h
    src/frameless_window.cpp
    src/frameless_window.h
    src/http_server.cpp
    src/http_server.h
)

target_include_directories(huoyin_launcher PRIVATE
    "${CEF_SDK_DIR}"
)

target_link_libraries(huoyin_launcher PRIVATE
    "${CEF_SDK_DIR}/win32/release/libcef.lib"
    "${CEF_SDK_DIR}/win32/release/VS2017/libcef_dll_wrapper.lib"
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

# 构建后复制 CEF 运行时、Flash 插件、Vue UI 产物到可执行目录
add_custom_command(TARGET huoyin_launcher POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CEF_RUNTIME_DIR}" "$<TARGET_FILE_DIR:huoyin_launcher>"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${NARUTO_ROOT}/third_party/pepflashplayer.dll"
            "$<TARGET_FILE_DIR:huoyin_launcher>/pepflashplayer.dll"
    COMMAND ${CMAKE_COMMAND} -E copy_directory
            "${NARUTO_ROOT}/app/ui/dist"
            "$<TARGET_FILE_DIR:huoyin_launcher>/ui/dist"
    COMMENT "复制 CEF 运行时、Flash 插件与 Vue UI 到可执行目录"
)
