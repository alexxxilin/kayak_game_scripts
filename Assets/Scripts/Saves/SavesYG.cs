using UnityEngine;
using System;
using System.Collections.Generic;

namespace YG
{
    public partial class SavesYG
    {
        // =====================================================================
        // ОСНОВНЫЕ СЧЕТЧИКИ (ExampleCharacterController)
        // =====================================================================
        public double coinsCollected = 0;
        public long rocketsCollected = 0;
        public int silverCoins = 0;
        public float timeUntilAdSpinAvailable;
        
        // =====================================================================
        // КОЛЕСО ФОРТУНЫ (FortuneWheel)
        // =====================================================================
        public int fortuneWheelSpins = 5;
        public float timeUntilFreeSpin = 3600f;
        public bool adsDisabled = false;
        
        // Новые поля для UI колеса фортуны
        public int spinCounter = 0;
        public float spinTimer = 0f;
        
        // =====================================================================
        // МИРОВАЯ СИСТЕМА (WorldSystemManager)
        // =====================================================================
        public string currentLocationID = "base_location";
        public float maxAchievedVirtualHeight = 0f;
        public int purchasedWingIndex = -1;
        public int equippedWingIndex = -1;
        public int trophiesCollected = 0;
        
        // 🔑 СОХРАНЕНИЕ КУПЛЕННЫХ ТЕЛЕПОРТОВ (по TriggerID)
        public List<string> purchasedTeleportTriggers = new List<string>();
        
        // Состояние каждой панели телепорта (для общей панели)
        public List<bool> teleportPanelUnlocked = new List<bool>();
        public int openedTeleportsCount = 0;

        // =====================================================================
        // СИСТЕМА ПИТОМЦЕВ (PetSystem)
        // =====================================================================
        public List<PetSaveData> ownedPets = new List<PetSaveData>();
        public List<int> equippedPetIds = new List<int>();
        public int nextPetId = 0;
        public bool autoEquipPets = true;

        // =====================================================================
        // НАСТРОЙКИ ИГРОКА
        // =====================================================================
        public bool cursorLocked = true;

        // =====================================================================
        // СТАТИСТИКА ИГРЫ
        // =====================================================================
        public float totalPlayTime = 0f;
        public float lastSaveTimeUnix = 0f;

        // =====================================================================
        // ВЕРСИЯ СОХРАНЕНИЙ
        // =====================================================================
        public int saveVersion = 1;

        // =====================================================================
        // ⚡ НОВОЕ ПОЛЕ: награда за Telegram в мире
        // =====================================================================
        public bool worldTelegramRewardClaimed = false;

        // =====================================================================
        // 🔥 ФЛАГ: покупка была совершена (даже если не успела примениться)
        // =====================================================================
        public bool pendingAdsDisabled = false;

        // =====================================================================
        // 🔥 НОВОЕ ПОЛЕ: количество полных восхождений на лестницы
        // =====================================================================
        public int ladderCompletionCount = 0;

        // =====================================================================
        // 🎁 НОВОЕ ПОЛЕ: список активированных промокодов
        // =====================================================================
        public List<string> activatedPromoCodes = new List<string>();

        // =====================================================================
        // 👑 НОВОЕ ПОЛЕ: VIP-доступ (разблокировка всех VIP-горок)
        // =====================================================================
        public bool vipUnlocked = false;

        // =====================================================================
        // КОНСТРУКТОР (опционально, для инициализации коллекций)
        // =====================================================================
        public SavesYG()
        {
            if (purchasedTeleportTriggers == null)
                purchasedTeleportTriggers = new List<string>();
            
            if (teleportPanelUnlocked == null)
                teleportPanelUnlocked = new List<bool>();
            
            if (ownedPets == null)
                ownedPets = new List<PetSaveData>();
            
            if (equippedPetIds == null)
                equippedPetIds = new List<int>();
            
            if (activatedPromoCodes == null)
                activatedPromoCodes = new List<string>();
        }
    }
}