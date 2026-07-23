namespace AorusLcd.Core;

/// <summary>Panel modes from ucVga.dll <c>DisplayMode</c>: 0-2 built-in stats, 3-6 user content, 7 carousel.</summary>
public enum LcdMode
{
    Faith1 = 0,
    Faith2 = 1,
    Faith3 = 2,
    Image = 3,
    Text = 4,
    Gif = 5,
    ChibTime = 6,
    Carousel = 7,
}

/// <summary>Template kind for <c>SetImageTpl</c> (ucVga.dll <c>TPL_TYPE</c>).</summary>
public enum LcdTemplateType : byte
{
    Gif = 1,
    Image = 2,
    Pet = 3,
}
