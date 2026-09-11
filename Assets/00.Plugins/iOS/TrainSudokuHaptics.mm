// Taptic Engine bridge for Assets/01.Scripts/Game/Audio/Haptics.cs.
//
// UIFeedbackGenerator is not exposed by Unity: Handheld.Vibrate() is the legacy AudioServices buzz and has no
// Taptic variety at all. The generators are cached as statics and re-prepared after every trigger so a run of
// taps stays low-latency; they are never released, which is correct with ARC on or off.
//
// Everything here no-ops by itself on a device without a Taptic Engine (iPad, iPhone 6s and older) and when the
// player has System Haptics switched off, so the C# side needs no capability check.

#import <UIKit/UIKit.h>

// Mirrors HapticFeel in Haptics.cs.
static UIImpactFeedbackGenerator *_impact[5] = { nil, nil, nil, nil, nil };
static UISelectionFeedbackGenerator *_selection = nil;
static UINotificationFeedbackGenerator *_notification = nil;

static UIImpactFeedbackGenerator *TSImpactGenerator(int style)
{
    if (style < 0 || style > 4) style = 0;
    if (_impact[style] == nil)
    {
        UIImpactFeedbackStyle mapped = UIImpactFeedbackStyleLight;
        switch (style)
        {
            case 1: mapped = UIImpactFeedbackStyleMedium; break;
            case 2: mapped = UIImpactFeedbackStyleHeavy; break;
            case 3: // Soft and Rigid are iOS 13+; older systems fall back to Light.
                if (@available(iOS 13.0, *)) mapped = UIImpactFeedbackStyleSoft;
                break;
            case 4:
                if (@available(iOS 13.0, *)) mapped = UIImpactFeedbackStyleRigid;
                break;
            default: break;
        }

        _impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:mapped];
    }

    return _impact[style];
}

static UISelectionFeedbackGenerator *TSSelectionGenerator(void)
{
    if (_selection == nil) _selection = [[UISelectionFeedbackGenerator alloc] init];
    return _selection;
}

static UINotificationFeedbackGenerator *TSNotificationGenerator(void)
{
    if (_notification == nil) _notification = [[UINotificationFeedbackGenerator alloc] init];
    return _notification;
}

extern "C"
{
    void _TSHapticsPrepare(void)
    {
        [TSSelectionGenerator() prepare];
        [TSImpactGenerator(0) prepare];
    }

    void _TSHapticsImpact(int style)
    {
        UIImpactFeedbackGenerator *generator = TSImpactGenerator(style);
        [generator impactOccurred];
        [generator prepare];
    }

    void _TSHapticsSelection(void)
    {
        UISelectionFeedbackGenerator *generator = TSSelectionGenerator();
        [generator selectionChanged];
        [generator prepare];
    }

    void _TSHapticsNotification(int type)
    {
        UINotificationFeedbackType mapped = UINotificationFeedbackTypeSuccess;
        if (type == 1) mapped = UINotificationFeedbackTypeWarning;
        else if (type == 2) mapped = UINotificationFeedbackTypeError;

        UINotificationFeedbackGenerator *generator = TSNotificationGenerator();
        [generator notificationOccurred:mapped];
        [generator prepare];
    }
}
