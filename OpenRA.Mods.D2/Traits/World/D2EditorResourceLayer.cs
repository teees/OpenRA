#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.D2.Traits
{
	using CellContents = D2ResourceLayer.CellContents;
	using ClearSides = D2ResourceLayer.ClearSides;

	[Desc("Used to render spice with round borders.")]
	public class D2EditorResourceLayerInfo : EditorResourceLayerInfo
	{
		public override object Create(ActorInitializer init) { return new D2EditorResourceLayer(init.Self); }
	}

	public class D2EditorResourceLayer : EditorResourceLayer
	{
		public D2EditorResourceLayer(Actor self)
			: base(self) { }

		public override CellContents UpdateDirtyTile(CPos c)
		{
			var t = Tiles[c];

			// Empty tile
			if (t.Type == null)
			{
				t.Sprite = null;
				return t;
			}

			NetWorth -= t.Density * t.Type.Info.ValuePerUnit;

			t.Density = ResourceDensityAt(c);

			NetWorth += t.Density * t.Type.Info.ValuePerUnit;

			int index;
			var clear = FindClearSides(t.Type, c);
			if (clear == ClearSides.None)
			{
				var sprites = D2ResourceLayer.Variants[t.Variant];
				var frame = t.Density > t.Type.Info.MaxDensity / 2 ? 1 : 0;
				t.Sprite = t.Type.Variants.First().Value[sprites[frame]];
			}
			else if (D2ResourceLayer.SpriteMap.TryGetValue(clear, out index))
				t.Sprite = t.Type.Variants.First().Value[index];
			else
				t.Sprite = null;

			return t;
		}

		protected override string ChooseRandomVariant(ResourceType t)
		{
			return D2ResourceLayer.Variants.Keys.Random(Game.CosmeticRandom);
		}

		bool CellContains(CPos c, ResourceType t)
		{
			return Tiles.Contains(c) && Tiles[c].Type == t;
		}

		ClearSides FindClearSides(ResourceType t, CPos p)
		{
			var ret = ClearSides.None;
			if (!CellContains(p + new CVec(0, -1), t))
				ret |= ClearSides.Top | ClearSides.TopLeft | ClearSides.TopRight;

			if (!CellContains(p + new CVec(-1, 0), t))
				ret |= ClearSides.Left | ClearSides.TopLeft | ClearSides.BottomLeft;

			if (!CellContains(p + new CVec(1, 0), t))
				ret |= ClearSides.Right | ClearSides.TopRight | ClearSides.BottomRight;

			if (!CellContains(p + new CVec(0, 1), t))
				ret |= ClearSides.Bottom | ClearSides.BottomLeft | ClearSides.BottomRight;

			if (!CellContains(p + new CVec(-1, -1), t))
				ret |= ClearSides.TopLeft;

			if (!CellContains(p + new CVec(1, -1), t))
				ret |= ClearSides.TopRight;

			if (!CellContains(p + new CVec(-1, 1), t))
				ret |= ClearSides.BottomLeft;

			if (!CellContains(p + new CVec(1, 1), t))
				ret |= ClearSides.BottomRight;

			return ret;
		}
	}
}
