using System.Runtime.CompilerServices;
using UnityEngine;

public static class RiverColors
{
    public static Color32 River => new(0, 0, 255, 255);
    public static Color32 Source => new(255, 0, 0, 255);
    public static Color32 Mouth => new(0, 255, 0, 255);
    public static Color32 MouthSecondary => new(255, 255, 0, 255);
    public static Color32 Water => new(0, 0, 0, 255);
    public static Color32 Blank => new(0, 0, 0, 0);
    public static Color32 ConnectionIn => new(0, 255, 255, 255);
    public static Color32 ConnectionInPoint => new(255, 0, 255, 255);
    public static Color32 ConnectionOut => new(128, 255, 255, 255);
    public static Color32 ConnectionOutPoint => new(255, 128, 255, 255);

    public static bool IsRiver(Color32 color) => 
        CesColorUtilities.ColorEquals(color, River) || 
        CesColorUtilities.ColorEquals(color, Source) || 
        CesColorUtilities.ColorEquals(color, Mouth) || 
        CesColorUtilities.ColorEquals(color, MouthSecondary) || 
        CesColorUtilities.ColorEquals(color, ConnectionIn) ||
        CesColorUtilities.ColorEquals(color, ConnectionInPoint) ||
        CesColorUtilities.ColorEquals(color, ConnectionOut) ||
        CesColorUtilities.ColorEquals(color, ConnectionOutPoint);

    public static bool IsNotRiver(Color32 color) => !IsRiver(color);

    public static bool IsMouth(Color32 color) =>
        CesColorUtilities.ColorEquals(color, Mouth) ||
        CesColorUtilities.ColorEquals(color, MouthSecondary);

    public static bool IsConnection(Color32 color) =>
        CesColorUtilities.ColorEquals(color, ConnectionIn) ||
        CesColorUtilities.ColorEquals(color, ConnectionOut);

    public static bool IsConnectionPoint(Color32 color) =>
        CesColorUtilities.ColorEquals(color, ConnectionInPoint) ||
        CesColorUtilities.ColorEquals(color, ConnectionOutPoint);
}

public struct RiversColorsInts
{
    public int River;
    public int Source;
    public int Mouth;
    public int MouthSecondary;
    public int Water;
    public int Blank;
    public int ConnectionIn;
    public int ConnectionInPoint;
    public int ConnectionOut;
    public int ConnectionOutPoint;

    public static RiversColorsInts Create() => new RiversColorsInts
    {
        River = RiverColors.River.ToIndex(),
        Source = RiverColors.Source.ToIndex(),
        Mouth = RiverColors.Mouth.ToIndex(),
        MouthSecondary = RiverColors.MouthSecondary.ToIndex(),
        Water = RiverColors.Water.ToIndex(),
        Blank = RiverColors.Blank.ToIndex(),
        ConnectionIn = RiverColors.ConnectionIn.ToIndex(),
        ConnectionInPoint = RiverColors.ConnectionInPoint.ToIndex(),
        ConnectionOut = RiverColors.ConnectionOut.ToIndex(),
        ConnectionOutPoint = RiverColors.ConnectionOutPoint.ToIndex(),
    };

    public readonly bool IsRiver(int color) =>
        color == River ||
        color == Source ||
        color == Mouth ||
        color == MouthSecondary ||
        color == ConnectionIn ||
        color == ConnectionInPoint ||
        color == ConnectionOut ||
        color == ConnectionOutPoint;

    public readonly bool IsNotRiver(int color) => !IsRiver(color);

    public readonly bool IsMouth(int color) =>
        color == Mouth ||
        color == MouthSecondary;

    public readonly bool IsConnection(int color) =>
        color == ConnectionIn ||
        color == ConnectionOut;

    public readonly bool IsConnectionPoint(int color) =>
        color == ConnectionInPoint ||
        color == ConnectionOutPoint;
}