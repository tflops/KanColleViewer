﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fiddler;
using Grabacr07.KanColleWrapper.Internal;
using Grabacr07.KanColleWrapper.Models.Raw;

namespace Grabacr07.KanColleWrapper.Models
{
	/// <summary>
	/// 母港に所属している艦娘を表します。
	/// </summary>
	public class Ship : RawDataWrapper<kcsapi_ship2>, IIdentifiable
	{
		private readonly Homeport homeport;

		/// <summary>
		/// この艦娘を識別する ID を取得します。
		/// </summary>
		public int Id
		{
			get { return this.RawData.api_id; }
		}

		/// <summary>
		/// 艦娘の種類に基づく情報を取得します。
		/// </summary>
		public ShipInfo Info { get; private set; }

		public int SortNumber
		{
			get { return this.RawData.api_sortno; }
		}

		/// <summary>
		/// 艦娘の現在のレベルを取得します。
		/// </summary>
		public int Level
		{
			get { return this.RawData.api_lv; }
		}

		/// <summary>
		/// 艦娘がロックされているかどうかを示す値を取得します。
		/// </summary>
		public bool IsLocked
		{
			get { return this.RawData.api_locked == 1; }
		}

		/// <summary>
		/// 艦娘の現在の累積経験値を取得します。
		/// </summary>
		public int Exp
		{
			get { return this.RawData.api_exp.Get(0) ?? 0; }
		}

		/// <summary>
		/// この艦娘が次のレベルに上がるために必要な経験値を取得します。
		/// </summary>
		public int ExpForNextLevel
		{
			get { return this.RawData.api_exp.Get(1) ?? 0; }
		}

		/// <summary>
		/// 耐久値を取得します。
		/// </summary>
		public LimitedValue HP { get; private set; }

		/// <summary>
		/// 燃料を取得します。
		/// </summary>
		public LimitedValue Fuel { get; private set; }

		/// <summary>
		/// 弾薬を取得します。
		/// </summary>
		public LimitedValue Bull { get; private set; }

		/// <summary>
		/// 火力ステータス値を取得します。
		/// </summary>
		public ModernizableStatus Firepower { get; private set; }

		/// <summary>
		/// 雷装ステータス値を取得します。
		/// </summary>
		public ModernizableStatus Torpedo { get; private set; }

		/// <summary>
		/// 対空ステータス値を取得します。
		/// </summary>
		public ModernizableStatus AA { get; private set; }

		/// <summary>
		/// 装甲ステータス値を取得します。
		/// </summary>
		public ModernizableStatus Armer { get; private set; }

		/// <summary>
		/// 運のステータス値を取得します。
		/// </summary>
		public ModernizableStatus Luck { get; private set; }

		/// <summary>
		/// Anti-Submarine stat with and without equipment.
		/// </summary>
		public LimitedValue AntiSub { get; private set; }

		/// <summary>
		/// Line of Sight stat with and without equipment.
		/// </summary>
		public LimitedValue LineOfSight { get; private set; }

		/// <summary>
		/// Evasion stat with and without equipment.
		/// </summary>
		public LimitedValue Evasion { get; private set; }

		/// <summary>
		/// 火力・雷装・対空・装甲のすべてのステータス値が最大値に達しているかどうかを示す値を取得します。
		/// </summary>
		public bool IsMaxModernized
		{
			get { return this.Firepower.IsMax && this.Torpedo.IsMax && this.AA.IsMax && this.Armer.IsMax; }
		}

		/// <summary>
		/// 現在のコンディション値を取得します。
		/// </summary>
		public int Condition
		{
			get { return this.RawData.api_cond; }
		}

		/// <summary>
		/// コンディションの種類を示す <see cref="ConditionType" /> 値を取得します。
		/// </summary>
		public ConditionType ConditionType
		{
			get { return ConditionTypeHelper.ToConditionType(this.RawData.api_cond); }
		}

		/// <summary>
		/// For visually generated elements. "[Lv.00]   Name"
		/// </summary>
		public string LvName
		{
			get { return "[Lv." + this.Level + "]  \t" + this.Info.Name; }
		}

		/// <summary>
		/// For visually generated elements. 
		/// "Name           [Lv.00]"
		/// "Long Name      [Lv.00]"    
		/// </summary>
		public string NameLv
		{
			get { return string.Format("{0, -20} [Lv.{1}]", this.Info.Name, this.Level); }
		}

		public SlotItem[] SlotItems { get; private set; }
		public int[] OnSlot { get; private set; }

		internal Ship(Homeport parent, kcsapi_ship2 rawData)
			: base(rawData)
		{
			this.homeport = parent;

			this.Info = KanColleClient.Current.Master.Ships[rawData.api_ship_id] ?? ShipInfo.Dummy;
			this.HP = new LimitedValue(this.RawData.api_nowhp, this.RawData.api_maxhp, 0);
			this.Fuel = new LimitedValue(this.RawData.api_fuel, this.Info.RawData.api_fuel_max, 0);
			this.Bull = new LimitedValue(this.RawData.api_bull, this.Info.RawData.api_bull_max, 0);

			if (this.RawData.api_kyouka.Length >= 5)
			{
				this.Firepower = new ModernizableStatus(this.Info.RawData.api_houg, this.RawData.api_kyouka[0]);
				this.Torpedo = new ModernizableStatus(this.Info.RawData.api_raig, this.RawData.api_kyouka[1]);
				this.AA = new ModernizableStatus(this.Info.RawData.api_tyku, this.RawData.api_kyouka[2]);
				this.Armer = new ModernizableStatus(this.Info.RawData.api_souk, this.RawData.api_kyouka[3]);
				this.Luck = new ModernizableStatus(this.Info.RawData.api_luck, this.RawData.api_kyouka[4]);
			}

			this.SlotItems = this.RawData.api_slot.Select(id => this.homeport.SlotItems[id]).Where(x => x != null).ToArray();
			this.OnSlot = this.RawData.api_onslot;

			// Minimum removes equipped values.
			int EqAntiSub = 0, EqEvasion = 0, EqLineOfSight = 0;

			foreach (SlotItem item in this.SlotItems)
			{
				if (item == null)
					continue;

				EqAntiSub += item.Info.RawData.api_tais;
				EqEvasion += item.Info.RawData.api_houk;
				EqLineOfSight += item.Info.RawData.api_saku;
			}

			this.AntiSub = new LimitedValue(this.RawData.api_taisen[0], this.RawData.api_taisen[1], this.RawData.api_taisen[0] - EqAntiSub);
			this.Evasion = new LimitedValue(this.RawData.api_kaihi[0], this.RawData.api_kaihi[1], this.RawData.api_kaihi[0] - EqEvasion);
			this.LineOfSight = new LimitedValue(this.RawData.api_sakuteki[0], this.RawData.api_sakuteki[1], this.RawData.api_sakuteki[0] - EqLineOfSight);
		}


		public override string ToString()
		{
			return string.Format("ID = {0}, Name = \"{1}\", ShipType = \"{2}\", Level = {3}", this.Id, this.Info.Name, this.Info.ShipType.Name, this.Level);
		}
	}
}
