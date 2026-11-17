#include "flowerlib.h"

static int32_t discount_basis_points(int32_t quantity)
{
    if (quantity >= 50)
    {
        return 1000;
    }

    if (quantity >= 20)
    {
        return 500;
    }

    return 0;
}

FS_API int64_t fs_reorder_cost_cents(int32_t quantity, int32_t unit_cost_cents)
{
    if (quantity < 1 || quantity > FS_MAX_QUANTITY)
    {
        return -1;
    }

    if (unit_cost_cents < 1 || unit_cost_cents > FS_MAX_UNIT_COST_CENTS)
    {
        return -1;
    }

    int64_t gross = (int64_t)quantity * (int64_t)unit_cost_cents;
    int64_t payable_basis_points = 10000 - discount_basis_points(quantity);

    return (gross * payable_basis_points + 5000) / 10000;
}
