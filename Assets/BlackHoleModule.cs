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

    private Transform[] _swirlsVisible;
    private Transform[] _swirlsActive;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved = false;

    private readonly int[][] _grid = new[] { 3, 4, 1, 0, 2, 3, 1, 2, 0, 4, 1, 3, 0, 2, 4, 1, 2, 3, 4, 0, 3, 2, 4, 2, 1, 3, 0, 0, 1, 4, 4, 0, 0, 1, 3, 4, 2, 2, 1, 3, 1, 2, 1, 3, 0, 0, 4, 3, 4, 2, 4, 0, 2, 3, 4, 1, 3, 0, 2, 1, 2, 1, 3, 1, 3, 0, 4, 4, 0, 2, 2, 4, 4, 0, 0, 2, 1, 1, 3, 3, 0, 1, 3, 4, 2, 2, 0, 4, 3, 1, 0, 3, 2, 4, 1, 4, 3, 1, 2, 0 }
        .Split(10).Select(gr => gr.ToArray()).ToArray();

    sealed class BlackHoleBombInfo
    {
        public List<BlackHoleModule> Modules = new List<BlackHoleModule>();
        public List<int> SolutionCode;
        public int DigitsEntered = 0;
        public int DigitsExpected;
        public BlackHoleModule LastDigitEntered;
    }

    private static readonly Dictionary<string, BlackHoleBombInfo> _infos = new Dictionary<string, BlackHoleBombInfo>();
    private BlackHoleBombInfo _info;
    private int _digitsEntered;
    private int _digitsExpected;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        _swirlsActive = new Transform[49];
        _swirlsVisible = new Transform[49];
        for (int i = 0; i < 49; i++)
        {
            _swirlsActive[i] = Instantiate(SwirlTemplate);
            _swirlsActive[i].parent = SwirlContainer.transform;
            _swirlsActive[i].localPosition = new Vector3(0, 0, 0);
            var scale = Rnd.Range(.99f, 1.05f);
            _swirlsActive[i].localScale = new Vector3(scale, scale, scale);
            _swirlsActive[i].GetComponent<MeshRenderer>().material.mainTexture = SwirlTextures[i / 7];
            _swirlsVisible[i] = _swirlsActive[i];
        }
        Destroy(SwirlTemplate.gameObject);

        var ser = Bomb.GetSerialNumber();
        if (!_infos.ContainsKey(ser))
            _infos[ser] = new BlackHoleBombInfo();
        _info = _infos[ser];
        _info.Modules.Add(this);

        _lastTime = (int) Bomb.GetTime();
        _lastSolved = 0;
        _digitsEntered = 0;
        _digitsExpected = 7;

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

        //_info.SolutionCode.Clear();
        //for (int i = 0; i < 7; i++)
        //    _info.SolutionCode.Add(0);
        //for (int i = 0; i < 7; i++)
        //    _info.SolutionCode.Add(4);
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
                    if (_swirlsVisible[i * 7 + j] != null)
                        _swirlsVisible[i * 7 + j].localEulerAngles = new Vector3(0, 0, angles[i] + 360f / 7 * j);
            }
            yield return null;
        }
    }

    private List<string> _events = new List<string>();
    private int _lastTime = 0;
    private int _lastSolved = 0;

    private void HoleInteractEnded()
    {
        if (!_isSolved && _events.Count(e => e == "down") > _events.Count(e => e == "up"))
        {
            _events.Add("up");
            checkEvents();
        }
    }

    private bool HoleInteract()
    {
        if (!_isSolved)
        {
            if (_events.All(e => e == "tick"))
                Audio.PlaySoundAtTransform("BlackHoleInput2", Selectable.transform);
            _events.Add("down");
            checkEvents();
        }
        return false;
    }

    private void Update()
    {
        for (int i = (_digitsExpected - _digitsEntered) * 7; i < 49; i++)
        {
            if (_swirlsActive[i] != null)
            {
                StartCoroutine(DisappearSwirl(i));
                _swirlsActive[i] = null;
            }
        }

        if (!_isSolved)
        {
            var time = (int) Bomb.GetTime();
            if (time != _lastTime)
            {
                _events.Add("tick");
                checkEvents();
                _lastTime = time;
            }

            var solved = Bomb.GetSolvedModuleNames().Where(mn => mn != "Black Hole").Count();
            if (solved != _lastSolved)
            {
                _lastSolved = solved;
                if (_info.LastDigitEntered == this)
                {
                    Debug.LogFormat(@"[Black Hole #{0}] You solved another module, so 2 digits are slashed from the code.", _moduleId);
                    _info.LastDigitEntered = null;
                    _info.DigitsExpected = Math.Max(_info.DigitsEntered + 1, _info.DigitsExpected - 2);
                    _digitsExpected = Math.Max(_digitsEntered + 1, _digitsExpected - 2);
                }
            }
        }
    }

    private IEnumerator DisappearSwirl(int ix)
    {
        var swirl = _swirlsVisible[ix];
        var startValue = swirl.localScale.x;
        var endValue = .9f;
        const float duration = 1f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            var size = (elapsed / duration) * (endValue - startValue) + startValue;
            swirl.localScale = new Vector3(size, size, size);
        }

        _swirlsVisible[ix] = null;
        Destroy(swirl.gameObject);
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
            _events.Clear();
            _events.Add("tick");
        }
        else
        {
            Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is correct.", _moduleId, digit);
            _events.Clear();
            _events.Add("tick");
            _info.DigitsEntered++;
            _info.LastDigitEntered = this;

            _digitsEntered++;
            if (_digitsEntered == _digitsExpected)
            {
                Audio.PlaySoundAtTransform("BlackHoleSuck", Selectable.transform);
                Module.HandlePass();
                _isSolved = true;
            }
            else
                Audio.PlaySoundAtTransform("BlackHoleSuckShort", Selectable.transform);
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Specify when to hold, release, tap, or wait for a timer tick in the correct order, for example: “!{0} hold, tick, release” or “!{0} tap, tick, tap”.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (_isSolved)
            yield break;

        var actions = new List<object>();
        foreach (var piece in command.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim().ToLowerInvariant()))
        {
            switch (piece)
            {
                case "hold":
                case "down":
                    actions.Add(new Action(() => { Selectable.OnInteract(); }));
                    actions.Add(new WaitForSeconds(.1f));
                    break;

                case "release":
                case "up":
                    actions.Add(new Action(() => { Selectable.OnInteractEnded(); }));
                    actions.Add(new WaitForSeconds(.1f));
                    break;

                case "tap":
                case "click":
                    actions.Add(new Action(() => { Selectable.OnInteract(); }));
                    actions.Add(new WaitForSeconds(.1f));
                    actions.Add(new Action(() => { Selectable.OnInteractEnded(); }));
                    actions.Add(new WaitForSeconds(.1f));
                    break;

                case "tick":
                case "wait":
                    actions.Add(new Func<object>(() =>
                    {
                        var time = (int) Bomb.GetTime();
                        return new WaitUntil(() => (int) Bomb.GetTime() != time);
                    }));
                    actions.Add(new WaitForSeconds(.1f));
                    break;

                default:
                    yield break;
            }
        }

        foreach (var action in actions)
        {
            if (action is Action)
                ((Action) action)();
            else if (action is Func<object>)
                yield return ((Func<object>) action)();
            else
                yield return action;
        }
    }
}