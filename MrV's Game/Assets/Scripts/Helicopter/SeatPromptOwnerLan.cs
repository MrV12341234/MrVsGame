using System.Collections.Generic;
using TMPro;

//this script is used so the two vehicle scripts talk to eachother
public static class SeatPromptOwnerLan
{
    private static readonly Dictionary<TMP_Text, object> owners = new Dictionary<TMP_Text, object>();

    public static void Show(object owner, TMP_Text text, string message)
    {
        if (text == null)
            return;

        owners[text] = owner;
        text.text = message;
    }

    public static void Hide(object owner, TMP_Text text)
    {
        if (text == null)
            return;

        if (owners.TryGetValue(text, out object currentOwner) && currentOwner == owner)
        {
            text.text = "";
            owners.Remove(text);
        }
    }
}