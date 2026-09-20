namespace SHCDESE.Interop.Enums;

/// <summary>
/// Currently known mapped AI Lord Messages
/// Rat (german) for reference
/// </summary>
public enum AILordMessageType : int
{
    IncomingMessage = 0,
    Taunt1 = 1,             // Unverschämter Flegel. Ich werde Eurem Treiben mit Vergnügen ein Ende bereiten.
    Taunt2 = 2,             // Nun seid Ihr besorgt, was? Meine Truppen werden Euch bald überwältigen ...
    Taunt3 = 3,             // Habt Ihr Angst? Die Streitmacht von De Puce wird Euch hinwegfegen wie eine Fliege vom Brot.
    Taunt4 = 4,             // Ihr habt Euch mit dem mächtigen De Puce angelegt, und bald schon werde ich Eure jämmerliche Armee zertrampeln.
    AngrySiegeLost = 5,     // Womit habe ich das verdient?? Ich ... ich ... ich hätte Euch VERNICHTEN sollen ...
    AngryCastleDamaged = 6, // aka SK_ANGRY_CASTLE_DAMAGED, german_rat=Arrraragh, meine stümperhaften Truppen haben ... einfach ... meinen ... Plan nicht verstanden ... arrggh...
    Defeat = 7,             // Bitte, nein bitte, tut mir nicht weh, bitte nicht ...
    NervPreSiege = 8,       // Kein Grund zur Panik, Ratte, kein Grund zur Panik, alles wird gut ... jawohl ...
    NervWeak = 9,           // Tja, es läuft recht gut für Euch, was? Das - äh - das kann sich ja auch noch ändern ... 
    VictoryGood = 10,       // Gewonnen, gewonnen!! Hähähähähääääää!!!!!!!
    VictoryHarass = 11,     // Ich hatte mein Vorgehen gut geplant! Ihr könnt Euch nicht mit mir messen!
    KillPlayer = 12,        // Ein weiterer Sieg meiner ruhmreichen Armee. Ihr seid Geschichte.
    KillNpc = 13,            // Die Welt steht mir offen ... und Ihr seid mir im Weg!
    RequestGoods = 14,      // Mein Meister benötigt die folgenden Güter. Sofort.
    ThankGoods = 15,        // Der Meister lässt Euch wissen, dass Eure Güter eingetroffen sind.
    DieAlly = 16,           // Ich habe doch TATSÄCHLICH schon WIEDER verloren ...
    CongratsOnKill = 17,    // Mein Meister gratuliert Euch zu Eurem Triumph.
    BoastOfKill = 18,       // Der Meister lässt Euch wissen, dass er einen unserer Gegner zermürbt hat.
    AllyNeedHelp = 19,      // Der Meister fordert Eure sofortige Unterstützung.
    MerryChristmas = 20,    // Der Meister wünscht Euch fröhliche Weihnachten.
    Unk21 = 21,             // 21 (UNUSED?)
    Unk22 = 22,             // 22 (UNUSED?)
    About2Siege = 23,       // Der Meister steht kurz davor, eine der feindlichen Burgen zu belagern.
    CantAttack = 24,        // Der Meister lässt Euch wissen, dass er unmöglich angreifen kann.
    WontAttack = 25,        // Der Meister lässt Euch wissen, dass er heute nicht angreifen wird.
    CantHelp = 26,          // Der Meister lässt Euch wissen, dass er Euch nicht helfen kann.
    WontHelp = 27,          // Der Meister lässt Euch wissen, dass er Euch nicht helfen wird.
    NotSendingGoods = 28,   // Mein Meister wird die Güter, um die Ihr gebeten habt, nicht senden.
    SentGoods = 29,         // Mein Meister hat Euch Eure Vorräte geschickt.
    TeamWinning = 30,       // Mein Meister glaubt, dass wir diesen Krieg gewinnen.
    TeamLosing = 31,        // Mein Meister glaubt, dass wir diesen Krieg verlieren.
    WillSendTroops = 32,    // Die Truppen meines Meisters wurden Euch zu Hilfe gesandt.
    WillAttackEnemy = 33,   // Mein Meister zeigt sich einverstanden.
    Nickname1 = 34,
    Nickname2 = 35,
    Nickname3 = 36,
    Nickname4 = 37,
    Nickname5 = 38,
    Nickname6 = 39,
    Nickname7 = 40,
    Nickname8 = 41
}