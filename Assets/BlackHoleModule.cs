using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackHole;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Black Hole
/// Created by Timwi
/// </summary>
public class BlackHoleModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public Texture[] SwirlTextures;
    public Transform SwirlTemplate;
    public Transform SwirlContainer;
    public KMSelectable Selectable;

    private Transform[] _swirls;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved = false;

    private readonly int[][] _grid = new[] { 3, 2, 3, 4, 2, 1, 4, 0, 4, 1, 0, 4, 1, 0, 4, 0, 4, 3, 2, 0, 2, 0, 2, 3, 3, 1, 2, 0, 4, 1, 2, 1, 3, 4, 0, 3, 4, 4, 1, 3, 0, 4, 2, 1, 1, 3, 0, 3, 3, 0, 1, 2, 0, 4, 2, 2, 4, 3, 2, 1, 3, 2, 4, 1, 3, 1, 4, 3, 0, 2, 0, 1, 4, 4, 0, 3, 1, 0, 3, 1, 2, 0, 3, 0, 1, 2, 2, 4, 1, 0, 4, 4, 2, 1, 0, 3, 2, 1, 3, 2 }
        .Split(10).Select(gr => gr.ToArray()).ToArray();

    sealed class BlackHoleBombInfo
    {
        public List<BlackHoleModule> Modules = new List<BlackHoleModule>();
        public List<int> SolutionCode;
        public int DigitsEntered = 0;
        public int DigitsExpected;
    }

    private static readonly Dictionary<string, BlackHoleBombInfo> _infos = new Dictionary<string, BlackHoleBombInfo>();
    private BlackHoleBombInfo _info;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        var colorNames = @"red,orange,yellow,green,cyan,blue,purple".Split(',');
        _swirls = new Transform[49];
        for (int i = 0; i < 49; i++)
        {
            _swirls[i] = Instantiate(SwirlTemplate);
            _swirls[i].parent = SwirlContainer.transform;
            _swirls[i].localPosition = new Vector3(0, 0, 0);
            _swirls[i].localScale = new Vector3(1, 1, 1);
            _swirls[i].GetComponent<MeshRenderer>().material.mainTexture = SwirlTextures[i / 7];
        }
        Destroy(SwirlTemplate.gameObject);

        var ser = Bomb.GetSerialNumber();
        if (!_infos.ContainsKey(ser))
            _infos[ser] = _info = new BlackHoleBombInfo();
        _info.Modules.Add(this);

        _lastTime = (int) Bomb.GetTime();

        StartCoroutine(MoveSwirls());
        StartCoroutine(ComputeSolutionCode(ser));
        Selectable.OnInteract = HoleInteract;
        Selectable.OnInteractEnded = HoleInteractEnded;
    }

    private IEnumerator ComputeSolutionCode(string serialNumber)
    {
        yield return null;

        if (_info.SolutionCode == null)
        {
            var x = serialNumber[2] - '0';
            var y = serialNumber[5] - '0';
            var dir = Bomb.GetPorts().Count() % 8;  // 0 = north, 1 = NE, etc.

            _info.DigitsExpected = _info.Modules.Count * 7;
            _info.SolutionCode = new List<int>();

            for (int i = 0; i < _info.DigitsExpected; i++)
            {
                var digit = 0;
                for (int j = 0; j < i + 1; j++)
                {
                    digit = (digit + _grid[y][x]) % 5;
                    if (dir == 1 || dir == 2 || dir == 3)
                        x = (x + 1) % 10;
                    else if (dir == 5 || dir == 6 || dir == 7)
                        x = (x + 9) % 10;
                    if (dir == 7 || dir == 0 || dir == 1)
                        y = (y + 9) % 10;
                    else if (dir == 3 || dir == 4 || dir == 5)
                        y = (y + 1) % 10;
                }
                _info.SolutionCode.Add(digit);
                dir = (dir + 1) % 8;
            }
        }

        Debug.LogFormat(@"[Black Hole #{0}] Solution code = {1}", _moduleId, _info.SolutionCode.JoinString());
    }

    private IEnumerator MoveSwirls()
    {
        var angles = new float[7];
        var rotationSpeeds = new float[7];
        for (int i = 0; i < 7; i++)
            rotationSpeeds[i] = Rnd.Range(10f, 30f);

        while (true)
        {
            for (int i = 0; i < 7; i++)
            {
                angles[i] -= rotationSpeeds[i] * Time.deltaTime;
                for (int j = 0; j < 7; j++)
                    _swirls[i * 7 + j].localEulerAngles = new Vector3(0, 0, angles[i] + 360f / 7 * j);
            }
            yield return null;
        }
    }

    private List<string> _events = new List<string>();
    private int _lastTime = 0;

    private void HoleInteractEnded()
    {
        Debug.LogFormat(@"<Black Hole #{0}> UP", _moduleId);
        if (!_isSolved && _events.Count(e => e == "down") > _events.Count(e => e == "up"))
        {
            _events.Add("up");
            checkEvents();
        }
    }

    private bool HoleInteract()
    {
        Debug.LogFormat(@"<Black Hole #{0}> DOWN", _moduleId);
        if (!_isSolved)
        {
            _events.Add("down");
            checkEvents();
        }
        return false;
    }

    private void Update()
    {
        if (!_isSolved)
        {
            var time = (int) Bomb.GetTime();
            if (time != _lastTime)
            {
                Debug.LogFormat(@"<Black Hole #{0}> TICK", _moduleId);
                _events.Add("tick");
                checkEvents();
                _lastTime = time;
            }
        }
    }

    private static readonly string[][] _eventsPerDigit = new[]
    {
        @"tick,down,tick,up,tick".Split(','),
        @"tick,down,up,tick,down,up,tick".Split(','),
        @"tick,down,up,tick,down,tick,up,tick".Split(','),
        @"tick,down,tick,up,down,tick,up,tick".Split(','),
        @"tick,down,tick,tick,up,tick".Split(',')
    };

    private void checkEvents()
    {
        while (_events.Count >= 2 && _events[0] == "tick" && _events[1] == "tick")
            _events.RemoveAt(0);

        var input = _eventsPerDigit.IndexOf(list => list.SequenceEqual(_events));
        if (input != -1)
            process(input);
        else if (_events.Count(e => e == "up") >= _events.Count(e => e == "down"))
        {
            var validPrefix = _eventsPerDigit.IndexOf(list => list.Take(_events.Count).SequenceEqual(_events));
            if (validPrefix == -1)
            {
                Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is not a valid digit.", _moduleId, _events.JoinString(", "));
                Module.HandleStrike();
                _events.Clear();
                _events.Add("tick");
            }
        }
    }

    private void process(int digit)
    {
        if (digit != _info.SolutionCode[_info.DigitsEntered])
        {
            Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is a wrong digit (I expected {2}).", _moduleId, digit, _info.SolutionCode[_info.DigitsEntered]);
            Module.HandleStrike();
        }
        else
        {
            Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is correct.", _moduleId, digit);
            _events.Clear();
            _events.Add("tick");
            _info.DigitsEntered++;
            if (_info.DigitsEntered == _info.DigitsExpected)
            {
                Module.HandlePass();
                _isSolved = true;
            }
        }
    }
}
