using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

using Engine;

namespace SuperAdventure
{
    public partial class SuperAdventure : Form
    {
        private Player _player;
        private Monster _currentMonster;

        private const string PLAYER_DATA_FILE_NAME = "PlayerData.xml";

        public int LoadDecide;

        public SuperAdventure()
        {
            InitializeComponent();

            SetCurrentPlayer(new Player(10, 10, 20, 0));

           
        }


        private void SetCurrentPlayer(Player player)
        {
            //remove current databindings
            lblHitPoints.DataBindings.Clear();
            lblGold.DataBindings.Clear();
            lblExperience.DataBindings.Clear();
            lblLevel.DataBindings.Clear();

            _player = player;

            MoveTo(World.LocationByID(World.LOCATION_ID_HOME));
            GivePlayerDefaultItems(player);

            UpdatePotionListInUI(); //shows potion at the beginning, if any
            //UpdateQuestListInUI();

            //bindings of labels to player properties, they update automatically
            lblHitPoints.DataBindings.Add("Text", player, "CurrentHitPoints");
            lblGold.DataBindings.Add("Text", player, "Gold");
            lblExperience.DataBindings.Add("Text", player, "ExperiencePoints");
            lblLevel.DataBindings.Add("Text", player, "Level");

            //linking of dataGridView to inventory item properties (datasource)
            dgvInventory.RowHeadersVisible = false;
            dgvInventory.AutoGenerateColumns = false;
            dgvInventory.DataSource = player.Inventory;

            dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                Width = 197,
                DataPropertyName = "Description"
            });

            dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Quantity",
                Width = 197,
                DataPropertyName = "Quantity"
            });

            //linking of dataGridView to player quest properties (data source)
            dgvQuests.RowHeadersVisible = false;
            dgvQuests.AutoGenerateColumns = false;
            dgvQuests.DataSource = player.Quests;

            dgvQuests.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                Width = 197,
                DataPropertyName = "Name"
            });

            dgvQuests.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Done?",
                Width = 197,
                DataPropertyName = "IsCompletedYesNo"
            });
        }

        private void GivePlayerDefaultItems(Player player)
        {
            // If the player's inventory does not contain a rusty sword, 
            // or a healing potion, give them to the player.
            if (player.Inventory.All(i => i.Details.ID != World.ITEM_ID_RUSTY_SWORD))
            {
                player.Inventory.Add(new InventoryItem(World.ItemByID(World.ITEM_ID_RUSTY_SWORD), 1));
            }

            if (player.Inventory.All(i => i.Details.ID != World.ITEM_ID_HEALING_POTION))
            {
                player.Inventory.Add(new InventoryItem(World.ItemByID(World.ITEM_ID_HEALING_POTION), 1));
            }
        }


        public void ResetByDeath()
        {
            BindingList<PlayerQuest> previousQuests = _player.Quests;
            List<Item> itemsToEnterLocations = World.Locations
                     .Where(x => x.ItemRequiredToEnter != null)
                     .Select(x => x.ItemRequiredToEnter)
                     .Distinct().ToList();

            List<InventoryItem> itemsToRemoveFromPlayerInventory = new List<InventoryItem>();

            foreach (InventoryItem ii in _player.Inventory)
            {
                foreach (Location location in World.Locations)
                {
                    // Look at all the itemsToEnterLocations.
                    // If none of them have an ID that matches the current inventory item,
                    // add the inventory item to the list of items to remove.
                    if (itemsToEnterLocations.All(i => i.ID != ii.Details.ID))
                    {
                        itemsToRemoveFromPlayerInventory.Add(ii);
                    }
                }
            }
            // Remove the non-location required items from the player's inventory.
            // This needs to be done seperately from finding the list of items to remove,
            // because you cannot remove objects from a collection
            // while inside a "foreach" loop that is looking at the items in the collection.
            // You would get an error that says "Collection was modified"

            foreach (InventoryItem item in itemsToRemoveFromPlayerInventory)
            {
               _player.Inventory.Remove(item); //keeps only items that are required to enter places
            }

            GivePlayerDefaultItems(_player);

            //_player.Quests = previousQuests; //keeps quest list and quests done when you die

            // Heal the player
            _player.CurrentHitPoints = _player.MaximumHitPoints;

            MoveTo(World.LocationByID(World.LOCATION_ID_HOME));
            ScrollToBottomOfMessages();
            rtbMessages.Text += Environment.NewLine + Environment.NewLine + Environment.NewLine;
        }

        private void btnNorth_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToNorth);
        }

        private void btnEast_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToEast);
        }

        private void btnSouth_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToSouth);
        }

        private void btnWest_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToWest);
        }


        public void MoveTo(Location newLocation)
        {
            if (!_player.HasRequiredItemToEnterThisLocation(newLocation))
            {
                ScrollToBottomOfMessages();
                rtbMessages.Text += "You must have a/an " + newLocation.ItemRequiredToEnter.Name + " to enter this location." + Environment.NewLine;
                return;

            }

            //Update the player's current location
            _player.CurrentLocation = newLocation;

            //Show/hide available movement buttons
            btnNorth.Visible = (newLocation.LocationToNorth != null);
            btnEast.Visible = (newLocation.LocationToEast != null);
            btnSouth.Visible = (newLocation.LocationToSouth != null);
            btnWest.Visible = (newLocation.LocationToWest != null);



            //Display current location name and description
            rtbLocation.Text = newLocation.Name + Environment.NewLine;
            rtbLocation.Text += newLocation.Description + Environment.NewLine;

            //Does the location have a quest?
            if (newLocation.QuestAvailableHere != null)
            {
                //see if the player already has the quest, and if they've completed it
                bool playerAlreadyHasQuest = _player.HasThisQuest(newLocation.QuestAvailableHere);
                bool playerAlreadyHasCompletedQuest = _player.CompletedThisQuest(newLocation.QuestAvailableHere);

                //See if the player already has the quest
                if (playerAlreadyHasQuest)
                {
                    //if the player has not completed the quest yet
                    if (!playerAlreadyHasCompletedQuest)
                    {
                        //See if the player has all the items needed to complete the quest 
                        bool playerHasAllTheItemsToCompleteQuest = _player.HasAllQuestCompletionItems(newLocation.QuestAvailableHere);

                        if (playerHasAllTheItemsToCompleteQuest)
                        {
                            //display message
                            ScrollToBottomOfMessages();
                            rtbMessages.Text += Environment.NewLine;
                            ScrollToBottomOfMessages();
                            rtbMessages.Text += "You complete the " + "«" + newLocation.QuestAvailableHere.Name + "»" + " quest." + Environment.NewLine;


                            //remove quest items from inventory
                            _player.RemoveQuestCompletionItems(newLocation.QuestAvailableHere);

                            //give quest rewards
                            ScrollToBottomOfMessages();
                            rtbMessages.Text += "You receive: " + Environment.NewLine;
                            rtbMessages.Text += newLocation.QuestAvailableHere.RewardExperiencePoints.ToString() + " experience points" + Environment.NewLine;
                            rtbMessages.Text += newLocation.QuestAvailableHere.RewardGold.ToString() + " gold" + Environment.NewLine;
                            rtbMessages.Text += newLocation.QuestAvailableHere.RewardItem.Name + Environment.NewLine;
                            rtbMessages.Text += Environment.NewLine;

                            _player.AddExperiencePoints(newLocation.QuestAvailableHere.RewardExperiencePoints);
                            _player.Gold += newLocation.QuestAvailableHere.RewardGold;



                            //Add reward item to the player's inventory
                            _player.AddItemToInventory(newLocation.QuestAvailableHere.RewardItem);

                            //mark the quest as completed
                            _player.MarkQuestCompleted(newLocation.QuestAvailableHere);

                            //Update UI text
                            UpdatePotionListInUI();

                            //lblGold.Text = _player.Gold.ToString();
                            //lblExperience.Text = _player.ExperiencePoints.ToString();

                        }
                    }
                }
                else
                {
                    //the player does not already have the quest

                    //display messages
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "You receive the " + "«" + newLocation.QuestAvailableHere.Name + "»" + " quest." + Environment.NewLine;
                    rtbMessages.Text += newLocation.QuestAvailableHere.Description + Environment.NewLine;
                    rtbMessages.Text += "To complete it, return with: " + Environment.NewLine;
                    foreach (QuestCompletionItem qci in newLocation.QuestAvailableHere.QuestCompletionItems)
                    {
                        if (qci.Quantity == 1)
                        {
                            ScrollToBottomOfMessages();
                            rtbMessages.Text += qci.Quantity.ToString() + " " + qci.Details.Name + Environment.NewLine;
                        }
                        else
                        {
                            ScrollToBottomOfMessages();
                            rtbMessages.Text += qci.Quantity.ToString() + " " + qci.Details.NamePlural + Environment.NewLine;
                        }
                    }
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += Environment.NewLine;

                    //Add the quest to the player quest's list
                    _player.Quests.Add(new PlayerQuest(newLocation.QuestAvailableHere));
                }
            }

            //Does the location have a monster?
            if (newLocation.MonsterLivingHere != null)
            {
                ScrollToBottomOfMessages();
                rtbMessages.Text += Environment.NewLine;
                ScrollToBottomOfMessages();
                rtbMessages.Text += "You see a " + newLocation.MonsterLivingHere.Name + "." + Environment.NewLine;

                //make a new monster, using the values from the standard monster in the World.Monster list
                Monster standardMonster = World.MonsterByID(newLocation.MonsterLivingHere.ID);

                _currentMonster = new Monster(standardMonster.ID, standardMonster.Name, standardMonster.MaximumDamage,
                    standardMonster.RewardExperiencePoints, standardMonster.RewardGold, standardMonster.CurrentHitPoints, standardMonster.MaximumHitPoints);

                foreach (LootItem lootItem in standardMonster.LootTable)
                {
                    _currentMonster.LootTable.Add(lootItem);
                }

                cboWeapons.Visible = true;
                btnUseWeapon.Visible = true;


            }
            else
            {
                _currentMonster = null;

                cboWeapons.Visible = false;
                btnUseWeapon.Visible = false;
            }

            //refresh player's quest list
            //UpdateQuestListInUI();

            //refresh player's weapons combobox
            UpdateWeaponsListInUI();

            //refresh player's potion combobox
            UpdatePotionListInUI();

            //Show/hide save button in presence of monsters
            btnSave.Visible = (_currentMonster == null);
        }

        //private void UpdateQuestListInUI()
        //{

        //    dgvQuests.RowHeadersVisible = false;

        //    dgvQuests.ColumnCount = 2;
        //    dgvQuests.Columns[0].Name = "Name";
        //    dgvQuests.Columns[0].Width = 209;
        //    dgvQuests.Columns[1].Name = "Cleared?";


        //    dgvQuests.Rows.Clear();

        //    foreach (PlayerQuest playerQuest in _player.Quests)
        //    {
        //        if (playerQuest.IsCompleted)
        //        {
        //            dgvQuests.Rows.Add(new[] { playerQuest.Details.Name, "Yes" });
        //        }
        //        else
        //        {
        //            dgvQuests.Rows.Add(new[] { playerQuest.Details.Name, "No" });
        //        }
        //    }
        //}

        private void UpdateWeaponsListInUI()
        {
            List<Weapon> weapons = new List<Weapon>();

            foreach (InventoryItem inventoryItem in _player.Inventory)
            {
                if (inventoryItem.Details is Weapon)
                {
                    if (inventoryItem.Quantity > 0)
                    {
                        weapons.Add((Weapon)inventoryItem.Details);
                    }
                }
            }

            if (weapons.Count == 0)
            {
                //the player has no weapons, hide the combobox and "use" button
                cboWeapons.Visible = false;
                btnUseWeapon.Visible = false;
            }
            else
            {
                cboWeapons.SelectedIndexChanged -= cboWeapons_SelectedIndexChanged;
                cboWeapons.DataSource = weapons;
                cboWeapons.SelectedIndexChanged += cboWeapons_SelectedIndexChanged;
                cboWeapons.DisplayMember = "Name";
                cboWeapons.ValueMember = "ID";

                if (_player.CurrentWeapon != null)
                {
                    cboWeapons.SelectedItem = _player.CurrentWeapon;
                }
                else
                {
                    cboWeapons.SelectedIndex = 0;
                }
            }
        }

        private void UpdatePotionListInUI()
        {
            List<HealingPotion> healingPotions = new List<HealingPotion>();

            foreach (InventoryItem inventoryItem in _player.Inventory)
            {
                if (inventoryItem.Details is HealingPotion)
                {
                    if (inventoryItem.Quantity > 0)
                    {
                        healingPotions.Add((HealingPotion)inventoryItem.Details);
                    }
                }
            }

            if (healingPotions.Count == 0)
            {
                //the player does not have any potions, hide the combobox and "use button
                cboPotions.Visible = false;
                btnUsePotion.Visible = false;
            }
            else
            {
                cboPotions.Visible = true;
                btnUsePotion.Visible = true;

                cboPotions.DataSource = healingPotions;
                cboPotions.DisplayMember = "Name";
                cboPotions.ValueMember = "ID";

                cboPotions.SelectedIndex = 0;
            }
        }

        private void btnUseWeapon_Click(object sender, EventArgs e)
        {
            //get the currently selected weapon from cboWeapons combobox
            Weapon currentWeapon = (Weapon)cboWeapons.SelectedItem;

            //Determine the amount of damage to do to the monster
            int damageToMonster = RandomNumberGenerator.NumberBetween(currentWeapon.MinimumDamage, currentWeapon.MaximumDamage) + _player.BaseDamage;

            //apply damage to monster's currenthitpoints
            _currentMonster.CurrentHitPoints -= damageToMonster;

            //display message
            ScrollToBottomOfMessages();
            rtbMessages.Text += "You hit the " + _currentMonster.Name + " for " + damageToMonster.ToString() + " points." + Environment.NewLine;

            //check if the monster is dead
            if (_currentMonster.CurrentHitPoints <= 0)
            {
                //Monster is dead

                ScrollToBottomOfMessages();
                rtbMessages.Text += "You defeated the " + _currentMonster.Name + "." + Environment.NewLine;
                ScrollToBottomOfMessages();
                rtbMessages.Text += Environment.NewLine;

                btnUseWeapon.Visible = false;
                cboWeapons.Visible = false;
                
                //Give player experience for killing the monster
                _player.AddExperiencePoints(_currentMonster.RewardExperiencePoints);

                ScrollToBottomOfMessages();
                rtbMessages.Text += "You receive " + _currentMonster.RewardExperiencePoints.ToString() + " experience points." + Environment.NewLine;

                //Give player gold for killing the monster
                _player.Gold += _currentMonster.RewardGold;
                if (_currentMonster.RewardGold == 1)
                {
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "You get " + _currentMonster.RewardGold.ToString() + " piece of gold." + Environment.NewLine;
                }
                else
                {
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "You get " + _currentMonster.RewardGold.ToString() + " pieces of gold." + Environment.NewLine;
                }

                //get random loot items from the monster
                List<InventoryItem> lootedItems = new List<InventoryItem>();

                //Add items to the lootedItems list, comparing a random number to the drop percentage
                foreach (LootItem lootItem in _currentMonster.LootTable)
                {
                    if (RandomNumberGenerator.NumberBetween(1, 100) <= lootItem.DropPercentage)
                    {
                        lootedItems.Add(new InventoryItem(lootItem.Details, 1));
                    }
                }

                //if no items were randomly selected, then add the default loot item(s)
                if (lootedItems.Count == 0)
                {
                    foreach (LootItem lootItem in _currentMonster.LootTable)
                    {
                        if (lootItem.IsDefaultItem)
                        {
                            lootedItems.Add(new InventoryItem(lootItem.Details, 1));
                        }
                    }
                }

                //add the looted items to the player's inventory
                foreach (InventoryItem inventoryItem in lootedItems)
                {
                    _player.AddItemToInventory(inventoryItem.Details);

                    if (inventoryItem.Quantity == 1)
                    {
                        ScrollToBottomOfMessages();
                        rtbMessages.Text += "You loot " + inventoryItem.Quantity.ToString() + " " + inventoryItem.Details.Name + Environment.NewLine;
                    }
                    else
                    {
                        ScrollToBottomOfMessages();
                        rtbMessages.Text += "You loot " + inventoryItem.Quantity.ToString() + inventoryItem.Details.NamePlural + Environment.NewLine;
                    }
                }

                UpdateWeaponsListInUI();
                UpdatePotionListInUI();

                _currentMonster = null;

                btnSave.Visible = (_currentMonster == null);
            }
            else
            {
                //monster is still alive

                //determine the amount of damage the monster does to the player
                int damageToPlayer = RandomNumberGenerator.NumberBetween(1, _currentMonster.MaximumDamage);

                //display message
                ScrollToBottomOfMessages();
                rtbMessages.Text += "The " + _currentMonster.Name + " did " + damageToPlayer.ToString() + " points of damage." + Environment.NewLine;

                //subtract damage from player 
                _player.CurrentHitPoints -= damageToPlayer;

                if (_player.CurrentHitPoints <= 0)
                {
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "The " + _currentMonster.Name + " killed you." + Environment.NewLine;

                    ResetByDeath();
                }
            }
        }

        private void cboWeapons_SelectedIndexChanged(object sender, EventArgs e)
        {
            _player.CurrentWeapon = (Weapon)cboWeapons.SelectedItem;
        }

        private void btnUsePotion_Click(object sender, EventArgs e)
        {
            //get the currently selected potion from the combobox
            HealingPotion potion = (HealingPotion)cboPotions.SelectedItem;

            //Add healing amount to the player's current hit points
            _player.CurrentHitPoints += potion.AmountToHeal;

            //current hit points cannot exceed player's max hit points
            if (_player.CurrentHitPoints > _player.MaximumHitPoints)
            {
                _player.CurrentHitPoints = _player.MaximumHitPoints;
            }

            //remove the potion from the player's inventory
            foreach (InventoryItem ii in _player.Inventory)
            {
                if (ii.Details.ID == potion.ID)
                {
                    ii.Quantity--;
                    break;
                }
            }
            //display message
            ScrollToBottomOfMessages();
            rtbMessages.Text += "You drink a " + potion.Name + " that replenishes " + potion.AmountToHeal.ToString() + " hit points." + Environment.NewLine;

            if (_currentMonster != null)
            {
                //monster gets their turn to attack

                //determine the amount of damage the monster does to the player
                int damageToPlayer = RandomNumberGenerator.NumberBetween(1, _currentMonster.MaximumDamage);

                //display message
                ScrollToBottomOfMessages();
                rtbMessages.Text += "The " + _currentMonster.Name + " did " + damageToPlayer.ToString() + " points of damage." + Environment.NewLine;

                //subtract damage from player 
                _player.CurrentHitPoints -= damageToPlayer;

                if (_player.CurrentHitPoints <= 0)
                {
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "The " + _currentMonster.Name + " killed you." + Environment.NewLine;

                    ResetByDeath();

                }
            }

            UpdatePotionListInUI();
        }

        private void ScrollToBottomOfMessages()
        {
            rtbMessages.SelectionStart = rtbMessages.Text.Length;
            rtbMessages.ScrollToCaret();
        }

        public void btnSave_Click(object sender, EventArgs e)
        {
            File.WriteAllText(PLAYER_DATA_FILE_NAME, _player.ToXmlString());
            ScrollToBottomOfMessages();
            rtbMessages.Text += "You saved the game." + Environment.NewLine;
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            Player newPlayer;

            mtbLoadDecide.Visible = true;
            btnLoadDecideNo.Visible = true;
            btnLoadDecideYes.Visible = true;

            if (File.Exists(PLAYER_DATA_FILE_NAME))
            {
                if (LoadDecide == 1)
                {
                    newPlayer = Player.CreatePlayerFromXmlString(File.ReadAllText(PLAYER_DATA_FILE_NAME));
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "You loaded the previous game save." + Environment.NewLine;
                }
                else if (LoadDecide == 2)
                {
                    newPlayer = Player.CreateDefaultPlayer();
                    GivePlayerDefaultItems(newPlayer);
                    ScrollToBottomOfMessages();
                    rtbMessages.Text += "Starting a new game." + Environment.NewLine;
                }
                else
                {
                    newPlayer = _player;
                }
            }
            else
            { 
                newPlayer = Player.CreateDefaultPlayer();
                GivePlayerDefaultItems(newPlayer);
                ScrollToBottomOfMessages();
                rtbMessages.Text += "No data saved. Starting a new game." + Environment.NewLine;
            }

            SetCurrentPlayer(newPlayer);

            MoveTo(_player.CurrentLocation);


        }

        private void btnLoadDecideYes_Click(object sender, EventArgs e)
        {
            LoadDecide = 1;
            mtbLoadDecide.Visible = false;
            btnLoadDecideYes.Visible = false;
            btnLoadDecideNo.Visible = false;
        }

        private void btnLoadDecideNo_Click(object sender, EventArgs e)
        {
            LoadDecide = 2;
            mtbLoadDecide.Visible = false;
            btnLoadDecideYes.Visible = false;
            btnLoadDecideNo.Visible = false;
        }
    }
}

