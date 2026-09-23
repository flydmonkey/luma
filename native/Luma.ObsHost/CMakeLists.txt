cmake_minimum_required(VERSION 3.20)
project(LumaObsHost LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(OBS_RUNTIME_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/obs/bin/64bit")
set(OBS_SRC_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/obs-src/libobs")
set(OBS_IMPLIB "${CMAKE_CURRENT_SOURCE_DIR}/lib/obs.lib")

if(NOT EXISTS "${OBS_IMPLIB}")
  message(FATAL_ERROR "obs.lib is missing. Run tools/build-obs-host.ps1 to generate the import library.")
endif()

add_executable(Luma.ObsHost
  main.cpp
  obs_session.cpp
)

target_include_directories(Luma.ObsHost PRIVATE
  "${CMAKE_CURRENT_SOURCE_DIR}/include"
  "${OBS_SRC_DIR}"
)

target_compile_definitions(Luma.ObsHost PRIVATE
  UNICODE
  _UNICODE
  NOMINMAX
  WIN32_LEAN_AND_MEAN
)

target_compile_options(Luma.ObsHost PRIVATE /utf-8)

target_link_libraries(Luma.ObsHost PRIVATE
  "${OBS_IMPLIB}"
  d3d11
  dxgi
  ole32
  user32
  gdi32
)

set_target_properties(Luma.ObsHost PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)
