using TMPro;
using UnityEngine;
using System.Collections;

public class CTFAnnouncementUILan : MonoBehaviour
{
    public static CTFAnnouncementUILan Instance;

    [SerializeField] private TMP_Text textBox;
    private Coroutine _routine;

    private void Awake()
    {
        Instance = this;
        if (textBox == null) textBox = GetComponent<TMP_Text>();
        if (textBox != null) textBox.text = "";
    }

    public void Show(string msg, float seconds = 2f)
    {
        if (textBox == null) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run(msg, seconds));
    }

    private IEnumerator Run(string msg, float seconds)
    {
        textBox.text = msg;
        yield return new WaitForSecondsRealtime(seconds);
        textBox.text = "";
        _routine = null;
    }
}