cmake_minimum_required(VERSION 3.28)

project(SoundScene.DaxBridge VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(SoundScene.DaxBridge SHARED
    src/exports.cpp
    src/probe.cpp
    src/dax_session.cpp
)

target_include_directories(SoundScene.DaxBridge PUBLIC include)

target_compile_definitions(SoundScene.DaxBridge PRIVATE
    SS_BRIDGE_EXPORTS
    UNICODE
    _UNICODE
    NOMINMAX
    WIN32_LEAN_AND_MEAN
)

if(MSVC)
    target_compile_options(SoundScene.DaxBridge PRIVATE /W4 /permissive- /EHsc)
endif()

# WindowsApp.lib provides RoGetActivationFactory / RoInitialize / WindowsCreateString.
target_link_libraries(SoundScene.DaxBridge PRIVATE WindowsApp)

set_target_properties(SoundScene.DaxBridge PROPERTIES
    OUTPUT_NAME "SoundScene.DaxBridge"
    PREFIX ""
)
