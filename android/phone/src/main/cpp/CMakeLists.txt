# SPDX-License-Identifier: AGPL-3.0-only
# libgtnative.so: libchiaki (chiaki-ng) + MediaCodec çözücü + JNI köprüsü.
# chiaki-ng'nin kök CMakeLists'i Android'de kendi uygulamasını (android/app) da derlemek ister; burada yalnız
# kütüphanesi (third-party + lib) eklenir ve kök dosyanın yaptığı hazırlık elle yapılır.
cmake_minimum_required(VERSION 3.22)
project(gtnative C CXX)

set(CHIAKI_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../../../../third-party/chiaki-ng" CACHE PATH "chiaki-ng kaynakları")
get_filename_component(CHIAKI_DIR "${CHIAKI_DIR}" ABSOLUTE)
if(NOT EXISTS "${CHIAKI_DIR}/lib/CMakeLists.txt")
	message(FATAL_ERROR "chiaki-ng bulunamadı: ${CHIAKI_DIR} (git clone --recurse-submodules https://github.com/streetpea/chiaki-ng third-party/chiaki-ng)")
endif()

# Kök CMakeLists'teki seçenekler: yalnız bağlantı, kayıt ve görüntü akışı.
set(CHIAKI_ENABLE_ANDROID ON)
set(CHIAKI_IS_SWITCH OFF)
set(CHIAKI_ENABLE_TESTS OFF)
set(CHIAKI_ENABLE_FFMPEG_DECODER OFF)
set(CHIAKI_ENABLE_PI_DECODER OFF)
set(CHIAKI_ENABLE_STEAM_SHORTCUT OFF)
set(CHIAKI_LIB_ENABLE_OPUS OFF)
set(CHIAKI_LIB_ENABLE_MBEDTLS OFF)
set(CHIAKI_LIB_OPENSSL_EXTERNAL_PROJECT OFF)
set(CHIAKI_USE_SYSTEM_JERASURE OFF)
set(CHIAKI_USE_SYSTEM_NANOPB OFF)
set(CHIAKI_USE_SYSTEM_CURL OFF)
set(CHIAKI_LIB_JSONC_EXTERNAL_PROJECT ON)
set(CHIAKI_LIB_MINIUPNPC_EXTERNAL_PROJECT ON)
set(CHIAKI_VERSION_MAJOR 1)
set(CHIAKI_VERSION_MINOR 10)
set(CHIAKI_VERSION_PATCH 1)
set(CHIAKI_VERSION ${CHIAKI_VERSION_MAJOR}.${CHIAKI_VERSION_MINOR}.${CHIAKI_VERSION_PATCH})

list(APPEND CMAKE_MODULE_PATH "${CHIAKI_DIR}/cmake")
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_POLICY_VERSION_MINIMUM 3.5)

# nanopb üreteci Python ister (protobuf ve grpcio-tools paketleriyle).
if(PYTHON_EXECUTABLE)
	set(Python_EXECUTABLE "${PYTHON_EXECUTABLE}" CACHE FILEPATH "" FORCE)
	set(Python3_EXECUTABLE "${PYTHON_EXECUTABLE}" CACHE FILEPATH "" FORCE)
endif()
find_package(PythonInterp 3 REQUIRED)

# OpenSSL: Google'ın hazır derlenmiş paketi (prefab, com.android.ndk.thirdparty:openssl). chiaki-ng'nin kendi
# yolu OpenSSL'i kaynaktan make ile derler; Windows'ta çalışmaz.
set(OPENSSL_USE_STATIC_LIBS TRUE)
find_package(OpenSSL 3 REQUIRED)

add_subdirectory("${CHIAKI_DIR}/third-party" chiaki-third-party EXCLUDE_FROM_ALL)

add_definitions(-DCHIAKI_VERSION_MAJOR=${CHIAKI_VERSION_MAJOR} -DCHIAKI_VERSION_MINOR=${CHIAKI_VERSION_MINOR}
		-DCHIAKI_VERSION_PATCH=${CHIAKI_VERSION_PATCH} -DCHIAKI_VERSION=\"${CHIAKI_VERSION}\")

# json-c: chiaki-ng'nin JsoncExternalProject'i başlıklar için sembolik bağ kurar; Windows'ta yetki ister.
# Aynı iş burada başlıklar kopyalanarak yapılır.
include(FetchContent)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(BUILD_APPS OFF CACHE BOOL "" FORCE)
set(DISABLE_EXTRA_LIBS ON CACHE BOOL "" FORCE)
set(DISABLE_BSON_OUTPUT ON CACHE BOOL "" FORCE)
FetchContent_Declare(json-c
	URL https://codeload.github.com/json-c/json-c/tar.gz/refs/tags/json-c-0.17-20230812
	URL_HASH SHA256=024d302a3aadcbf9f78735320a6d5aedf8b77876c8ac8bbb95081ca55054c7eb
)
FetchContent_MakeAvailable(json-c)
set(JSONC_PREFIX_INCLUDE_DIR "${CMAKE_BINARY_DIR}/_deps/json-c-prefix-include")
file(GLOB GT_JSONC_HEADERS "${json-c_SOURCE_DIR}/*.h")
file(COPY ${GT_JSONC_HEADERS} DESTINATION "${JSONC_PREFIX_INCLUDE_DIR}/json-c")

# miniupnpc: aynı nedenle (sembolik bağ) MiniupnpcExternalProject yerine burada.
set(UPNPC_BUILD_STATIC ON CACHE BOOL "" FORCE)
set(UPNPC_BUILD_SHARED OFF CACHE BOOL "" FORCE)
set(UPNPC_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(UPNPC_BUILD_SAMPLE OFF CACHE BOOL "" FORCE)
set(UPNPC_NO_INSTALL ON CACHE BOOL "" FORCE)
FetchContent_Declare(miniupnpc
	URL https://codeload.github.com/miniupnp/miniupnp/tar.gz/refs/tags/miniupnpc_2_2_8
	URL_HASH SHA256=3d0d49f761f09c1321ceb663d1b008825361d7bd242640c60ae467e82642502a
	SOURCE_SUBDIR miniupnpc
)
FetchContent_MakeAvailable(miniupnpc)
set(MINIUPNPC_PREFIX_INCLUDE_DIR "${CMAKE_BINARY_DIR}/_deps/miniupnpc-prefix-include")
file(GLOB GT_UPNPC_HEADERS "${miniupnpc_SOURCE_DIR}/miniupnpc/include/*.h")
file(COPY ${GT_UPNPC_HEADERS} DESTINATION "${MINIUPNPC_PREFIX_INCLUDE_DIR}/miniupnpc")

add_subdirectory("${CHIAKI_DIR}/lib" chiaki-lib EXCLUDE_FROM_ALL)

add_library(gtnative SHARED gt_native.c gt_jni.c)
target_compile_options(gtnative PRIVATE -Wall)
target_link_libraries(gtnative chiaki-lib mediandk log android)

# Android 15/16 devices with 16 KiB memory pages.
target_link_options(gtnative PRIVATE "-Wl,-z,max-page-size=16384" "-Wl,--exclude-libs,ALL")

