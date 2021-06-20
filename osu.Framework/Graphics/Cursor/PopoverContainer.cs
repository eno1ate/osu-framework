// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

#nullable enable

namespace osu.Framework.Graphics.Cursor
{
    public class PopoverContainer : CursorEffectContainer<PopoverContainer, IHasPopover>
    {
        private readonly Container content;
        private readonly Container<Popover> popoverContainer;

        private IHasPopover? target;
        private Popover? currentPopover => popoverContainer.LastOrDefault();

        protected override Container<Drawable> Content => content;

        public PopoverContainer()
        {
            InternalChildren = new Drawable[]
            {
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                popoverContainer = new Container<Popover>
                {
                    AutoSizeAxes = Axes.Both
                },
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            switch (e.Button)
            {
                case MouseButton.Left:
                    target = FindTargets().FirstOrDefault();
                    break;
            }

            return false;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);

            if (target == null)
                return;

            currentPopover?.Hide();

            var newPopover = target.GetPopover();
            if (newPopover == null)
                return;

            popoverContainer.Add(newPopover);
            Debug.Assert(currentPopover != null);
            currentPopover.Show();
            currentPopover.State.BindValueChanged(_ => cleanUpPopover(currentPopover));
        }

        private void cleanUpPopover(Popover popover)
        {
            if (popover.State.Value == Visibility.Hidden)
                popover.Expire();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updatePopoverPositioning();
        }

        /// <summary>
        /// The <see cref="Anchor"/>s to consider when auto-layouting the popover.
        /// <see cref="Anchor.Centre"/> is not included, as it is used as a fallback if any other anchor fails.
        /// </summary>
        private static readonly Anchor[] candidate_anchors =
        {
            Anchor.TopLeft,
            Anchor.TopCentre,
            Anchor.TopRight,
            Anchor.CentreLeft,
            Anchor.CentreRight,
            Anchor.BottomLeft,
            Anchor.BottomCentre,
            Anchor.BottomRight
        };

        private void updatePopoverPositioning()
        {
            if (target == null || currentPopover == null)
                return;

            var targetLocalQuad = ToLocalSpace(target.ScreenSpaceDrawQuad);

            Anchor bestAnchor = Anchor.Centre;
            float biggestArea = 0;

            // Reset the body position before proceeding, as it potentially affects the popover's BoundingBoxContainer size.
            currentPopover.Body.Position = new Vector2(0);

            foreach (var anchor in candidate_anchors)
            {
                // Compute how much free space is available on this side of the target.
                var availableSize = availableSizeAroundTargetForAnchor(targetLocalQuad, anchor);
                float area = availableSize.X * availableSize.Y;

                // If the free space is insufficient for the popover to fit in, do not consider this anchor further.
                if (availableSize.X < currentPopover.BoundingBoxContainer.DrawWidth || availableSize.Y < currentPopover.BoundingBoxContainer.DrawHeight)
                    continue;

                // The heuristic used to find the "best" anchor is the biggest area of free space available in the popover container
                // on the side of the anchor.
                if (area > biggestArea)
                {
                    biggestArea = area;
                    bestAnchor = anchor;
                }
            }

            currentPopover.PopoverAnchor = bestAnchor.Opposite();
            popoverContainer.Position = bestAnchor.PositionOnQuad(targetLocalQuad);

            // While the side has been chosen to maximise the area of free space available, that doesn't mean that the popover's body
            // will still fit in its entirety in the default configuration.
            // To avoid this, keep the arrow where it was, but offset the body so that it fits in the bounds of this container.
            var popoverContentLocalQuad = ToLocalSpace(currentPopover.Body.ScreenSpaceDrawQuad);
            if (popoverContentLocalQuad.TopLeft.X < 0)
                currentPopover.Body.X = -popoverContentLocalQuad.TopLeft.X;
            else if (popoverContentLocalQuad.BottomRight.X > DrawWidth)
                currentPopover.Body.X = DrawWidth - popoverContentLocalQuad.BottomRight.X;
            if (popoverContentLocalQuad.TopLeft.Y < 0)
                currentPopover.Body.Y = -popoverContentLocalQuad.TopLeft.Y;
            else if (popoverContentLocalQuad.BottomRight.Y > DrawHeight)
                currentPopover.Body.Y = DrawHeight - popoverContentLocalQuad.BottomRight.Y;
        }

        /// <summary>
        /// Computes the available size around the <paramref name="targetLocalQuad"/> on the side of it indicated by <paramref name="anchor"/>
        /// </summary>
        private Vector2 availableSizeAroundTargetForAnchor(Quad targetLocalQuad, Anchor anchor)
        {
            Vector2 availableSize = new Vector2();

            // left anchor = area to the left of the quad, right anchor = area to the right of the quad.
            // for horizontal centre assume we have the whole quad width to work with.
            if (anchor.HasFlagFast(Anchor.x0))
                availableSize.X = MathF.Max(0, targetLocalQuad.TopLeft.X);
            else if (anchor.HasFlagFast(Anchor.x2))
                availableSize.X = MathF.Max(0, DrawWidth - targetLocalQuad.BottomRight.X);
            else
                availableSize.X = DrawWidth;

            // top anchor = area above quad, bottom anchor = area below quad.
            // for vertical centre assume we have the whole quad height to work with.
            if (anchor.HasFlagFast(Anchor.y0))
                availableSize.Y = MathF.Max(0, targetLocalQuad.TopLeft.Y);
            else if (anchor.HasFlagFast(Anchor.y2))
                availableSize.Y = MathF.Max(0, DrawHeight - targetLocalQuad.BottomRight.Y);
            else
                availableSize.Y = DrawHeight;

            // the final size is the intersection of the X/Y areas.
            return availableSize;
        }
    }
}
