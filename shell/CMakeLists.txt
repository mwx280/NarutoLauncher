# 外壳（Shell）—— Qt 6.9.3 · x64（MSVC）
# 配置方式（x64 目标，MSVC 环境）：
#   cmake -S shell -B build/shell -G Ninja -DCMAKE_PREFIX_PATH="C:/Users/xiaowu/Dev/Qt/6.9.3/msvc2022_64"
#   cmake --build build/shell

cmake_minimum_required(VERSION 3.16)
project(naruto_shell VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

find_package(Qt6 REQUIRED COMPONENTS Widgets Network)

add_executable(naruto_shell WIN32
    src/main.cpp
    src/frameless_window.cpp
    src/frameless_window.h
    src/title_bar.cpp
    src/title_bar.h
    src/side_nav.cpp
    src/side_nav.h
    src/account.cpp
    src/account.h
)

target_link_libraries(naruto_shell PRIVATE
    Qt6::Widgets
    Qt6::Network
)

# 源码含中文注释，强制 MSVC 以 UTF-8 解析源码
if(MSVC)
    target_compile_options(naruto_shell PRIVATE /utf-8)
endif()
