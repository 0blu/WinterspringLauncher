using System.Runtime.InteropServices;

namespace WinterspringLauncher.Utils;

public static class UnixApi
{
    [DllImport("libc", SetLastError = true)]
    public static extern int chmod(string pathname, int mode);

    // user permissions
    public const int PERM_USR_R = 0x100;
    public const int PERM_USR_W = 0x80;
    public const int PERM_USR_X = 0x40;

    // group permission
    public const int PERM_GRP_R = 0x20;
    public const int PERM_GRP_W = 0x10;
    public const int PERM_GRP_X = 0x8;

    // other permissions
    public const int PERM_OTH_R = 0x4;
    public const int PERM_OTH_W = 0x2;
    public const int PERM_OTH_X = 0x1;
    
    public const int PERM_0777 =
        PERM_USR_R | PERM_USR_X | PERM_USR_W |
        PERM_GRP_R | PERM_GRP_X | PERM_GRP_W |
        PERM_OTH_R | PERM_OTH_X | PERM_OTH_W;
}
