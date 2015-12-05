#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Place the second actor in line to build more of the same at once (used for walls).")]
	public class LineBuildInfo : ITraitInfo
	{
		[Desc("The maximum allowed length of the line.")]
		public readonly int Range = 5;

		[Desc("LineBuildNode 'Types' to attach to.")]
		public readonly HashSet<string> NodeTypes = new HashSet<string> { "wall" };

		[Desc("Type of actor to use as segments between nodes (empty means same as this).")]
		public readonly string SegmentType = null;

		[Desc("Delete segments between nodes when node gets destroyed.")]
		public readonly bool SanitizeSegments = false;

		public object Create(ActorInitializer init) { return new LineBuild(init.Self, this); }
	}

	public class LineBuild : INotifyRemovedFromWorld
	{
		readonly LineBuildInfo Info;
		readonly Actor self;

		public LineBuild(Actor self, LineBuildInfo info)
		{
			Info = info;
			this.self = self;
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (Info.SanitizeSegments)
			{
				var self_position = self.World.Map.CellContaining(self.CenterPosition);
				foreach (var direction in new CVec[] {new CVec(-1,0), new CVec(0,-1), new CVec(1,0), new CVec(0,1)})
				{
					var position = new CPos(self_position.X, self_position.Y);
					for (var i = 0; i <= Info.Range; i++)
					{
						position += direction;
						var actors = self.World.ActorMap.GetActorsAt(position).Where(a => self != a && !a.IsDead && a.Info.Name == Info.SegmentType);
						if (actors.Any())
							foreach (var a in actors)
								a.Dispose();
						else
							break;
					}
				}

			}
		}
	}
}
