#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TS.Traits
{
	class AttackOrderPowerInfo : SupportPowerInfo
	{
		public override object Create(ActorInitializer init) { return new AttackOrderPower(init.Self, this); }
	}

	class AttackOrderPower : SupportPower, INotifyAddedToWorld, INotifyAttack
	{
		AttackOrderPowerInfo info;
		AttackBase attack;

		public AttackOrderPower(Actor self, AttackOrderPowerInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			Game.Sound.PlayToPlayer(manager.Self.Owner, Info.SelectTargetSound);
			self.World.OrderGenerator = new SelectAttackPowerTarget(order, manager, info.Cursor, MouseButton.Left, attack);
		}

		public override void Charged(Actor self, string key)
		{
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);
			attack.AttackTarget(Target.FromCell(self.World, order.TargetLocation), false, false, true);
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			attack = self.Trait<AttackBase>();
		}

		void INotifyAttack.Attacking(Actor self, Target target, Armament a, Barrel barrel)
		{
			self.World.IssueOrder(new Order("Stop", self, false));
		}
	}

	public class SelectAttackPowerTarget : IOrderGenerator
	{
		readonly SupportPowerManager manager;
		readonly string order;
		readonly string cursor;
		readonly string cursorBlocked;
		readonly MouseButton expectedButton;
		readonly AttackBase attack;

		public SelectAttackPowerTarget(string order, SupportPowerManager manager, string cursor, MouseButton button, AttackBase attack)
		{
			// Clear selection if using Left-Click Orders
			if (Game.Settings.Game.UseClassicMouseStyle)
				manager.Self.World.Selection.Clear();

			this.manager = manager;
			this.order = order;
			this.cursor = cursor;
			expectedButton = button;
			this.attack = attack;
			cursorBlocked = cursor + "-blocked";
		}

		public IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			world.CancelInputMode();
			if (mi.Button == expectedButton && IsValidTarget(world, cell))
				yield return new Order(order, manager.Self, false) { TargetLocation = cell, SuppressVisualFeedback = true };
		}

		public virtual void Tick(World world)
		{
			// Cancel the OG if we can't use the power
			if (!manager.Powers.ContainsKey(order))
				world.CancelInputMode();
		}

		bool IsValidTarget(World world, CPos cell)
		{
			return world.Map.Contains(cell) && attack.IsReachableTarget(Target.FromCell(world, cell), false);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }
		public IEnumerable<IRenderable> RenderAfterWorld(WorldRenderer wr, World world) { yield break; }
		public string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return IsValidTarget(world, cell) ? cursor : cursorBlocked;
		}
	}
}
