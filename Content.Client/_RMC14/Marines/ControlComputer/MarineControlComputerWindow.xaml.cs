﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.Marines.ControlComputer;

[GenerateTypedNameReferences]
public sealed partial class MarineControlComputerWindow : DefaultWindow
{
    public MarineControlComputerWindow()
    {
        RobustXamlLoader.Load(this);
    }
}
