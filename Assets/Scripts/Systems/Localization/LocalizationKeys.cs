/// <summary>
/// Tüm lokalizasyon string anahtarlarının merkezi sabitleri.
/// Kod içinde magic string kullanmak yerine bu sabitleri kullanın.
///
/// Kullanım:
///   string text = LocalizationManager.Get(LocalizationKeys.UI_START_BUTTON);
/// </summary>
public static class LocalizationKeys
{
    // ─────────────────────────────────────────────────────────────────────────
    // UI — Genel
    // ─────────────────────────────────────────────────────────────────────────
    public const string UI_START_BUTTON      = "ui.start_button";
    public const string UI_BACK_TO_MENU      = "ui.back_to_menu";
    public const string UI_SETTINGS          = "ui.settings";
    public const string UI_CLOSE             = "ui.close";
    public const string UI_CONFIRM           = "ui.confirm";
    public const string UI_CANCEL            = "ui.cancel";
    public const string UI_READY             = "ui.ready";
    public const string UI_GOLD_LABEL        = "ui.gold_label";
    public const string UI_LEVEL_LABEL       = "ui.level_label";

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Savaş Durumu
    // ─────────────────────────────────────────────────────────────────────────
    public const string BATTLE_PRESS_WAR     = "battle.press_war";
    public const string BATTLE_VICTORY       = "battle.victory";
    public const string BATTLE_ALL_DONE      = "battle.all_done";
    public const string BATTLE_LEVEL_CLEARED = "battle.level_cleared";
    public const string BATTLE_BASE_REWARD   = "battle.base_reward";

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Yenilgi Mesajları
    // ─────────────────────────────────────────────────────────────────────────
    public const string DEFEAT_0             = "defeat.0";
    public const string DEFEAT_1             = "defeat.1";
    public const string DEFEAT_2             = "defeat.2";
    public const string DEFEAT_3             = "defeat.3";
    public const string DEFEAT_4             = "defeat.4";
    public const string DEFEAT_5             = "defeat.5";
    public const string DEFEAT_6             = "defeat.6";

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Ayarlar Paneli
    // ─────────────────────────────────────────────────────────────────────────
    public const string SETTINGS_TITLE       = "settings.title";
    public const string SETTINGS_SFX         = "settings.sfx";
    public const string SETTINGS_MUSIC       = "settings.music";
    public const string SETTINGS_QUALITY     = "settings.quality";
    public const string SETTINGS_RESOLUTION  = "settings.resolution";
    public const string SETTINGS_RESET       = "settings.reset";
    public const string SETTINGS_RESET_CONFIRM = "settings.reset_confirm";
    public const string SETTINGS_RESET_YES   = "settings.reset_yes";
    public const string SETTINGS_RESET_NO    = "settings.reset_no";
    public const string SETTINGS_LANGUAGE    = "settings.language";

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Pawn Shop
    // ─────────────────────────────────────────────────────────────────────────
    public const string PAWNSHOP_BUY         = "pawnshop.buy";
    public const string PAWNSHOP_MAX_REACHED = "pawnshop.max_reached";
    public const string PAWNSHOP_NOT_ENOUGH  = "pawnshop.not_enough";
    public const string PAWNSHOP_COST        = "pawnshop.cost";

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Upgrade / Ekipman
    // ─────────────────────────────────────────────────────────────────────────
    public const string UPGRADE_TITLE        = "upgrade.title";
    public const string UPGRADE_WEAPON       = "upgrade.weapon";
    public const string UPGRADE_HELMET       = "upgrade.helmet";
    public const string UPGRADE_VEST         = "upgrade.vest";
    public const string UPGRADE_PANTS        = "upgrade.pants";
    public const string UPGRADE_SHIELD       = "upgrade.shield";
    public const string UPGRADE_EMPTY_SLOT   = "upgrade.empty_slot";
    public const string UPGRADE_DRAG_HINT    = "upgrade.drag_hint";

    // ─────────────────────────────────────────────────────────────────────────
    // Ekipman İsimleri
    // ─────────────────────────────────────────────────────────────────────────
    public const string ITEM_SWORD           = "item.sword";
    public const string ITEM_GUN             = "item.gun";
    public const string ITEM_SHIELD          = "item.shield";
    public const string ITEM_HELMET          = "item.helmet";
    public const string ITEM_VEST            = "item.vest";
    public const string ITEM_PANTS           = "item.pants";

    // ─────────────────────────────────────────────────────────────────────────
    // Genel Mesajlar
    // ─────────────────────────────────────────────────────────────────────────
    public const string COMMON_LOADING       = "common.loading";
    public const string COMMON_ERROR         = "common.error";
    public const string COMMON_YES           = "common.yes";
    public const string COMMON_NO            = "common.no";
}
