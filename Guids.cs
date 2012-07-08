// Guids.cs
// MUST match guids.h
using System;

namespace WellTyped.Haskell
{
    static class GuidList
    {
        public const string guidHaskellTest2PkgString = "67132972-3040-48d5-bc3c-401b3c71451d";
        public const string guidHaskellTest2CmdSetString = "0a525164-5c0e-4310-81cc-cb6d4950b824";

        public static readonly Guid guidHaskellTest2CmdSet = new Guid(guidHaskellTest2CmdSetString);
    };
}