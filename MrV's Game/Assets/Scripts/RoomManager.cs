using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;

[NetworkMode(NetworkMode.PHOTON)]
public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager Instance;
    
    public string roomCode = "Map1";
    public GameObject player;
    public Transform[] spawnPoints;
    [Space]
    public GameObject roomCamera;

    public string roomNameToJoin = "test";

    public GameObject Quiz;
    public GameObject correctAnswer;
    public GameObject wrongAnswer;
    private string currentName = "Chocolate";
    public bool showQuiz;

    [HideInInspector] public int correctAnswerCounter = 0;

    public void getCorrectAnswer()
    {
        correctAnswerCounter++;
        if (correctAnswerCounter == 3)
        {
            Quiz.SetActive(false);
            RespawnPlayer();
        } 
        StartCoroutine(showCorrectAnswer());
    }

    public void getWrongAnswer()
    {
        StartCoroutine(showWrongAnswer());
    }

    public void ShowQuiz()
    {
        if (showQuiz)
        {
            Quiz.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            correctAnswerCounter = 0;

            // Initialize the first question
            QuestionSetup setup = Quiz.GetComponentInChildren<QuestionSetup>();
            if (setup != null)
            {
                setup.InitializeNewQuestion();
            }
        }
        else
        {
            RespawnPlayer();
        }
    }

    IEnumerator showCorrectAnswer()
    {
        correctAnswer.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        correctAnswer.SetActive(false);
    }

    IEnumerator showWrongAnswer()
    {
        wrongAnswer.SetActive(true);
        yield return new WaitForSeconds(5f);
        wrongAnswer.SetActive(false);
    }

    private void Start()
    {
        // Room setup logic goes here
    }

    private void Awake()
    {
        Instance = this;
    }

    public void ChangeName(string _name)
    {
        if (!string.IsNullOrWhiteSpace(_name))
        {
            // Limit the name to 12 characters (characters is limited to 12 in inspector but this is failsafe)
            if (_name.Length > 12)
            {
                _name = _name.Substring(0, 12);
            }
            currentName = _name;
        }
    }

    public void ConnectToServer()
    {
        if (!GameMode.IsLAN)
        {
            PhotonNetwork.JoinOrCreateRoom(roomCode, null, null);
        }
        
    }

    public Vector3 GetRandomSpawnPos()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        return spawnPoint.position;
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room. Spawning player");
        PhotonNetwork.Instantiate(player.name, GetRandomSpawnPos(), Quaternion.identity);
        roomCamera.SetActive(false);
        PhotonNetwork.LocalPlayer.NickName = currentName;
    }

    public void RespawnPlayer()
    {
        PhotonNetwork.Instantiate(player.name, GetRandomSpawnPos(), Quaternion.identity);
    }
}
