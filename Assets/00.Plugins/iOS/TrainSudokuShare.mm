// Share-sheet bridge for Assets/01.Scripts/Game/ShareSheet.cs.
//
// Unity exposes no way to reach UIActivityViewController: there is no share API in UnityEngine at all, and the
// nearest thing, GUIUtility.systemCopyBuffer, only reaches the pasteboard. So the result card goes out through
// the system sheet here, and the C# side falls back to the clipboard everywhere that is not an iOS device.
//
// Two traps this handles, both of which are a hard crash rather than a misbehaviour:
//   - On iPad the sheet is a popover, and presenting a popover with no anchor throws. It is anchored to the
//     bottom centre of the Unity view, with no arrow, which is where a share sheet comes from on a phone anyway.
//   - Presenting on a view controller that is already presenting one does nothing and logs. The walk below finds
//     the topmost presented controller, so a sheet still appears if anything else is up.
//
// The memory management is written to compile correctly whether or not the generated Xcode project turns ARC on
// for this file; under manual retain/release the sheet is released after presenting, which retains it.

#import <UIKit/UIKit.h>

// Unity's own export: the view controller the game is rendering into.
extern UIViewController *UnityGetGLViewController();

static UIViewController *TSTopmostViewController(void)
{
    UIViewController *controller = UnityGetGLViewController();
    while (controller != nil && controller.presentedViewController != nil)
        controller = controller.presentedViewController;
    return controller;
}

extern "C"
{
    void _TSShareText(const char *utf8)
    {
        if (utf8 == NULL) return;

        NSString *text = [NSString stringWithUTF8String:utf8];
        if (text == nil || text.length == 0) return;

        // Unity may call this off the main thread, and UIKit refuses to be touched anywhere else.
        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController *presenter = TSTopmostViewController();
            if (presenter == nil) return;

            UIActivityViewController *sheet =
                [[UIActivityViewController alloc] initWithActivityItems:@[text] applicationActivities:nil];

            // iPad presents this as a popover; one with no source view throws on present.
            UIPopoverPresentationController *popover = sheet.popoverPresentationController;
            if (popover != nil)
            {
                popover.sourceView = presenter.view;
                popover.sourceRect = CGRectMake(CGRectGetMidX(presenter.view.bounds),
                                                CGRectGetMaxY(presenter.view.bounds), 0.0, 0.0);
                popover.permittedArrowDirections = 0;
            }

            [presenter presentViewController:sheet animated:YES completion:nil];

#if !__has_feature(objc_arc)
            // Presenting retains it, so this balances the alloc rather than freeing it out from under UIKit.
            [sheet release];
#endif
        });
    }
}
