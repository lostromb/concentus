static const char Test3DArray[4][2][5] =
{
    {
        {
            4,      6,     24,      7,      5
        },
        {
             0,      0,      2,      0,      0
        }
    },
    {
        {
             12,     28,     41,     13,     -4
        },
        {
            -9,     15,     42,     25,     14
        }
    },
    {
        {
             1,     -2,     62,     41,     -9
        },
        {
            -10,     37,     65,     -4,      3
        }
    },
    {
        {
             -6,      4,     66,      7,     -8
        },
        {
             16,     14,     38,     -3,     33
        }
    }
};

#include <stdio.h>

int main()
{
    printf("%d\r\n", Test3DArray[0][0][0]);
    printf("%d\r\n", Test3DArray[3][0][3]);
    printf("%d\r\n", Test3DArray[0][1][0]);
    printf("%d\r\n", Test3DArray[3][1][3]);
    

    int x = 3 < 4;
    x = !!x;
    x = 17;
    x = !!x;
    x = 0;
}