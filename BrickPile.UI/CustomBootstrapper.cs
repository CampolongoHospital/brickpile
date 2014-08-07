﻿using BrickPile.Core;
using BrickPile.Core.Conventions;
using BrickPile.Core.Hosting;

namespace BrickPile.Samples
{
    public class CustomBootstrapper : DefaultBrickPileBootstrapper
    {
        public override void ConfigureConventions(BrickPileConventions brickPileConventions) {
            brickPileConventions.VirtualPathProviderConventions.Register("Default",() => new NativeVirtualPathProvider());
        }
    }
}