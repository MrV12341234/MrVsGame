using System.Text;

public static class GamertagRoomNameBlockedWords
{
    private static readonly string[] blockedWords =
    {
        // Profanity / insults
        "fuck",
        "fucked",
        "fucker",
        "fucking",
        "motherfuck",
        "motherfucker",
        "fuk",
        "fucc",
        "fuker",
        "fukin",
        "phuck",
        "phuk",
        "fck",
        "fcuk",

        "shit",
        "bullshit",
        "shithead",
        "sh1t",
        "shyt",
        "shiit",

        "bitch",
        "bitches",
        "bitchass",
        "b1tch",
        "biatch",
        "bytch",

        "asshole",
        "jackass",
        "dumbass",
        "bastard",
        "douche",
        "douchebag",
        "damn",
        "wtf",
        "stfu",
        "dick",

        // Sexual / explicit
        "penis",
        "vagina",
        "pussy",
        "cunt",
        "twat",
        "boobs",
        "b00bs",
        "boobies",
        "titty",
        "titties",
        "boner",
        "erection",
        "blowjob",
        "bl0w",
        "handjob",
        "rimjob",
        "deepthroat",
        "porn",
        "porno",
        "xxx",
        "nude",
        "nudes",
        "naked",
        "slut",
        "whore",
        "prick",

        // Abuse / predatory terms
        "rape",
        "rapist",
        "molest",
        "molester",
        "pedophile",
        "paedophile",
        "childmolester",

        // Slurs / hateful language
        "fag",
        "faggot",
        "dyke",
        "retard",
        "retarded",
        "spastic",
        "spaz",
        "nigger",
        "nigga",
        "n1gger",
        "n1gga",
        "chink",
        "gook",
        "kike",
        "wetback",
        "beaner",
        "paki",
        "tranny",

        // Extremist / self-harm related
        "nazi",
        "hitler",
        "heilhitler",
        "whitepower",
        "kkk",
        "killyourself",
        "kys",
        "suicide",

        // Requested blocked numbers
        "91",
        "78",
        "vlp",
        "cs"
    };

    private static readonly string[] normalizedBlockedWords = BuildNormalizedBlockedWords();

    public static bool ContainsBlockedWord(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string normalizedInput = Normalize(input);

        for (int i = 0; i < normalizedBlockedWords.Length; i++)
        {
            string blocked = normalizedBlockedWords[i];

            if (string.IsNullOrEmpty(blocked))
                continue;

            if (normalizedInput.Contains(blocked))
                return true;
        }

        return false;
    }

    private static string[] BuildNormalizedBlockedWords()
    {
        string[] result = new string[blockedWords.Length];

        for (int i = 0; i < blockedWords.Length; i++)
        {
            result[i] = Normalize(blockedWords[i]);
        }

        return result;
    }

    private static string Normalize(string value)
    {
        value = value.ToLowerInvariant();

        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            // Remove spaces and punctuation so:
            // "bad word", "bad-word", and "BADWORD" all get caught.
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }

        return sb.ToString();
    }
}