using System;

namespace Amenonegames.AutoComponentProperty
{
    [Flags]
    internal enum GetFrom
    {
        This  = 1,
        Children = 1 << 1,
        Parent = 1 << 2,
    }
}