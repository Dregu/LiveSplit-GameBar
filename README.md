# LiveSplit-GameBar
LiveSplit timer overlay for Xbox Game Bar I made as a sort of first hello world project...

Requires https://github.com/LiveSplit/LiveSplit.Server running on localhost in the default port. Pretty crude, just polls the current time all the time really fast. The widget crashes (it's a feature) when it can't connect to livesplit server, so start that first and then restart the widget in gamebar. Doesn't show up on OBS game capture or anything, it's for your eyes only if you only have a single monitor or something. Might add splits, controls or settings later, it seems super easy...

![Example](https://raw.githubusercontent.com/Dregu/LiveSplit-GameBar/master/LiveSplit-GameBar.png)
<p align='center'>It's this thing ^ in the middle.</p>

## Installation

I don't have a store dev account so it's not in widget store. You can build it in VS2019 or maybe install the test release (it's self signed, figure it out) if you manage to trust the packages certificate (add to "Trusted people" maybe? I dunno.)
