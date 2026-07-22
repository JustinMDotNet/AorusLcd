namespace AorusLcd.Core;

/// <summary>
/// Panel display modes (ucVga.dll <c>DisplayMode</c> enum). 0-2 are Gigabyte's
/// built-in stat screens; 3-6 are user content; 7 is the built-in carousel.
/// </summary>
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
