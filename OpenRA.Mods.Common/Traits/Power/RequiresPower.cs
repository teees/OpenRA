#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Needs power to operate.")]
	class RequiresPowerInfo : UpgradableTraitInfo, ITraitInfo, Requires<UpgradeManagerInfo>
	{
		[UpgradeGrantedReference]
		[Desc("The upgrades to grant while the actor is powered.")]
		public readonly string[] PoweredUpgrades = { };

		public override object Create(ActorInitializer init) { return new RequiresPower(init.Self, this); }
	}

	class RequiresPower : UpgradableTrait<RequiresPowerInfo>, IDisable, ITick, INotifyOwnerChanged, ISync
	{
		readonly UpgradeManager manager;
		readonly RequiresPowerInfo info;
		PowerManager playerPower;
		bool wasDisabled = true;

		public RequiresPower(Actor self, RequiresPowerInfo info)
			: base(info)
		{
			this.info = info;
			playerPower = self.Owner.PlayerActor.Trait<PowerManager>();
			manager = self.Trait<UpgradeManager>();
		}

		public bool Disabled
		{
			get { return playerPower.PowerProvided < playerPower.PowerDrained && !IsTraitDisabled; }
		}

		void ITick.Tick(Actor self)
		{
			var isDisabled = self.IsDisabled();
			if (wasDisabled && !isDisabled)
				foreach (var up in info.PoweredUpgrades)
					manager.GrantUpgrade(self, up, this);
			else if (!wasDisabled && isDisabled)
				foreach (var up in info.PoweredUpgrades)
					manager.RevokeUpgrade(self, up, this);
			wasDisabled = isDisabled;
		}

		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			playerPower = newOwner.PlayerActor.Trait<PowerManager>();
		}
	}
}
