#ifndef FLOWERLIB_H
#define FLOWERLIB_H

#include <stdint.h>

#ifdef _WIN32
#define FS_API __declspec(dllexport)
#else
#define FS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define FS_MAX_QUANTITY 1000000
#define FS_MAX_UNIT_COST_CENTS 1000000

FS_API int64_t fs_reorder_cost_cents(int32_t quantity, int32_t unit_cost_cents);

#ifdef __cplusplus
}
#endif

#endif
