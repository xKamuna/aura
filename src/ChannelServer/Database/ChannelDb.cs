﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Data;
using Aura.Data.Database;
using Aura.Shared.Database;
using Aura.Shared.Mabi;
using Aura.Shared.Mabi.Const;
using Aura.Shared.Util;
using MySql.Data.MySqlClient;
using Aura.Channel.World.Entities;

namespace Aura.Channel.Database
{
	public class ChannelDb
	{
		public static readonly ChannelDb Instance = new ChannelDb();

		private ChannelDb()
		{
		}

		/// <summary>
		/// Returns account incl all characters or null, if it doesn't exist.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public Account GetAccount(string accountId)
		{
			var account = new Account();

			using (var conn = AuraDb.Instance.Connection)
			{
				// Account
				// ----------------------------------------------------------
				var mc = new MySqlCommand("SELECT * FROM `accounts` WHERE `accountId` = @accountId", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);

				using (var reader = mc.ExecuteReader())
				{
					if (!reader.HasRows)
						return null;

					reader.Read();

					account.Id = reader.GetStringSafe("accountId");
					account.SessionKey = reader.GetInt64("sessionKey");
					account.Authority = reader.GetByte("authority");
				}

				// Characters
				// ----------------------------------------------------------
				mc = new MySqlCommand("SELECT * FROM `characters` WHERE `accountId` = @accountId", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);

				using (var reader = mc.ExecuteReader())
				{
					while (reader.Read())
					{
						var character = this.GetCharacter<Character>(reader.GetInt64("entityId"), "characters");
						if (character == null)
							continue;

						// Add GM titles for the characters of authority 50+ accounts
						if (account.Authority >= Authority.GameMaster) character.Titles.Add(60000, true); // GM
						if (account.Authority >= Authority.Admin) character.Titles.Add(60001, true); // devCat

						account.Characters.Add(character);
					}
				}

				// Pets
				// ----------------------------------------------------------
				mc = new MySqlCommand("SELECT * FROM `pets` WHERE `accountId` = @accountId", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);

				using (var reader = mc.ExecuteReader())
				{
					while (reader.Read())
					{
						var character = this.GetCharacter<Pet>(reader.GetInt64("entityId"), "pets");
						if (character == null)
							continue;

						account.Pets.Add(character);
					}
				}

				// Partners
				// ----------------------------------------------------------
				mc = new MySqlCommand("SELECT * FROM `partners` WHERE `accountId` = @accountId", conn);
				mc.Parameters.AddWithValue("@accountId", accountId);

				using (var reader = mc.ExecuteReader())
				{
					while (reader.Read())
					{
						var character = this.GetCharacter<Pet>(reader.GetInt64("entityId"), "partners");
						if (character == null)
							continue;

						account.Pets.Add(character);
					}
				}
			}

			return account;
		}

		/// <summary>
		/// Returns creature with entityId from table.
		/// </summary>
		/// <typeparam name="TCreature"></typeparam>
		/// <param name="entityId"></param>
		/// <returns></returns>
		private TCreature GetCharacter<TCreature>(long entityId, string table) where TCreature : PlayerCreature, new()
		{
			using (var conn = AuraDb.Instance.Connection)
			{
				var mc = new MySqlCommand("SELECT * FROM `" + table + "` AS c INNER JOIN `creatures` AS cr ON c.creatureId = cr.creatureId WHERE `entityId` = @entityId", conn);
				mc.Parameters.AddWithValue("@entityId", entityId);

				var character = new TCreature();

				using (var reader = mc.ExecuteReader())
				{
					if (!reader.Read())
						return null;

					character.EntityId = reader.GetInt64("entityId");
					character.CreatureId = reader.GetInt64("creatureId");
					character.Name = reader.GetStringSafe("name");
					character.Server = reader.GetStringSafe("server");
					character.Race = reader.GetInt32("race");
					character.DeletionTime = reader.GetDateTimeSafe("deletionTime");
					character.SkinColor = reader.GetByte("skinColor");
					character.EyeType = reader.GetByte("eyeType");
					character.EyeColor = reader.GetByte("eyeColor");
					character.MouthType = reader.GetByte("mouthType");
					character.Height = reader.GetFloat("height");
					character.Weight = reader.GetFloat("weight");
					character.Upper = reader.GetFloat("upper");
					character.Lower = reader.GetInt32("lower");
					character.Color1 = reader.GetUInt32("color1");
					character.Color2 = reader.GetUInt32("color2");
					character.Color3 = reader.GetUInt32("color3");
					character.Region = reader.GetInt32("region");
					var x = reader.GetInt32("x");
					var y = reader.GetInt32("y");
					character.SetPosition(x, y);
				}

				var items = this.GetItems(character.CreatureId, conn);
				foreach (var item in items)
					character.Inventory.ForceAdd(item, item.Info.Pocket);

				return character;
			}
		}

		private List<Item> GetItems(long creatureId, MySqlConnection conn)
		{
			var result = new List<Item>();

			var mc = new MySqlCommand("SELECT * FROM `items` WHERE `creatureId` = @creatureId", conn);
			mc.Parameters.AddWithValue("@creatureId", creatureId);

			using (var reader = mc.ExecuteReader())
			{
				while (reader.Read())
				{
					var itemId = reader.GetInt32("itemId");

					var item = new Item(itemId);
					item.EntityId = reader.GetInt64("entityId");
					item.Info.Pocket = (Pocket)reader.GetInt32("pocket");
					item.Info.X = reader.GetInt32("x");
					item.Info.Y = reader.GetInt32("y");
					item.Info.Color1 = reader.GetUInt32("color1");
					item.Info.Color3 = reader.GetUInt32("color2");
					item.Info.Color3 = reader.GetUInt32("color3");
					item.Info.Amount = reader.GetUInt16("amount");
					item.Info.State = reader.GetByte("state");
					item.OptionInfo.Price = reader.GetInt32("price");
					item.OptionInfo.SellingPrice = reader.GetInt32("sellPrice");
					item.OptionInfo.Durability = reader.GetInt32("durability");
					item.OptionInfo.DurabilityMax = reader.GetInt32("durabilityMax");
					item.OptionInfo.DurabilityNew = reader.GetInt32("durabilityNew");
					item.OptionInfo.AttackMin = reader.GetUInt16("attackMin");
					item.OptionInfo.AttackMax = reader.GetUInt16("attackMax");
					item.OptionInfo.Balance = reader.GetByte("balance");
					item.OptionInfo.Critical = reader.GetByte("critical");
					item.OptionInfo.Defense = reader.GetInt32("defense");
					item.OptionInfo.Protection = reader.GetInt16("protection");
					item.OptionInfo.EffectiveRange = reader.GetInt16("range");
					item.OptionInfo.AttackSpeed = (AttackSpeed)reader.GetByte("attackSpeed");
					item.OptionInfo.Experience = reader.GetInt16("experience");

					result.Add(item);
				}
			}

			return result;
		}
	}

	public static class Authority
	{
		public const int Player = 0;
		public const int VIP = 1;
		public const int GameMaster = 50;
		public const int Admin = 99;
	}
}
