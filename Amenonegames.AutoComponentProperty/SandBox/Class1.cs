using System;
using AutoComponentProperty;

namespace SandBox
{
    public partial class Class1
    {
        [CompoProp(GetFrom.This)]private int _myInt;
        [CompoProp(GetFrom.Parent)]private int _myInt2;
        [CompoProp(GetFrom.Children)]private int _myInt3;
        [CompoProp]private int[] _myInt4;
    }
}