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
    public Texture[] PlanetTextures;
    public MeshRenderer ImageTemplate;
    public GameObject ContainerTemplate;
    public TextMesh TextTemplate;
    public Transform SwirlContainer;
    public KMSelectable Selectable;

    private Transform[] _swirlsVisible;
    private Transform[] _swirlsActive;
    private Texture[][] _planetTextures;

    private PlanetInfo _activePlanet = null;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved = false;

    const float _planetSize = .5f;

    private readonly int[][] _grid = new[] { 3, 4, 1, 0, 2, 3, 1, 2, 0, 4, 1, 3, 0, 2, 4, 1, 2, 3, 4, 0, 3, 2, 4, 2, 1, 3, 0, 0, 1, 4, 4, 0, 0, 1, 3, 4, 2, 2, 1, 3, 1, 2, 1, 3, 0, 0, 4, 3, 4, 2, 4, 0, 2, 3, 4, 1, 3, 0, 2, 1, 2, 1, 3, 1, 3, 0, 4, 4, 0, 2, 2, 4, 4, 0, 0, 2, 1, 1, 3, 3, 0, 1, 3, 4, 2, 2, 0, 4, 3, 1, 0, 3, 2, 4, 1, 4, 3, 1, 2, 0 }
        .Split(10).Select(gr => gr.ToArray()).ToArray();

    private readonly Color[] _colors = new[] {
        new Color(0xe7/255f, 0x09/255f, 0x09/255f),
        new Color(0xed/255f, 0x80/255f, 0x0c/255f),
        new Color(0xde/255f, 0xda/255f, 0x16/255f),
        new Color(0x17/255f, 0xb1/255f, 0x29/255f),
        new Color(0x10/255f, 0xa0/255f, 0xa8/255f),
        new Color(0x28/255f, 0x26/255f, 0xff/255f),
        new Color(0xbb/255f, 0x0d/255f, 0xb0/255f)
    };

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

        _planetTextures = new Texture[12][];
        for (int i = 0; i < 12; i++)
            _planetTextures[i] = new Texture[7];
        foreach (var tx in PlanetTextures)
        {
            var p1 = tx.name.IndexOf('-');
            var p2 = tx.name.LastIndexOf('-');
            _planetTextures[int.Parse(tx.name.Substring(p1 + 1, p2 - p1 - 1))][int.Parse(tx.name.Substring(p2 + 1))] = tx;
        }

        _swirlsActive = new Transform[49];
        _swirlsVisible = new Transform[49];
        for (int i = 0; i < 49; i++)
        {
            var ct = Instantiate(ContainerTemplate).transform;
            ct.parent = SwirlContainer.transform;
            ct.localPosition = new Vector3(0, 0, 0);
            var scale = Rnd.Range(.99f, 1.05f);
            ct.localScale = new Vector3(scale, scale, scale);
            ct.gameObject.SetActive(true);

            var mr = Instantiate(ImageTemplate);
            mr.material.mainTexture = SwirlTextures[i / 7];
            mr.material.renderQueue = 2700 + i;
            mr.transform.parent = ct;
            mr.transform.localPosition = new Vector3((250 - 201 - 70 / 2) / 500f, (250 - 31 - 32 / 2) / 500f, 0);
            mr.transform.localScale = new Vector3(70f / 500, 32f / 500, 1);
            mr.gameObject.SetActive(true);

            _swirlsActive[i] = _swirlsVisible[i] = ct;
        }

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

        Bomb.OnBombExploded += delegate { _infos.Clear(); };
        Bomb.OnBombSolved += delegate
        {
            // This check is necessary because this delegate gets called even if another bomb in the same room got solved instead of this one
            if (Bomb.GetSolvedModuleNames().Count == Bomb.GetSolvableModuleNames().Count)
                _infos.Remove(ser);
        };
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
                    if (_swirlsVisible[i * 7 + j] != null)
                        _swirlsVisible[i * 7 + j].localEulerAngles = new Vector3(0, 0, angles[i] + 360f / 7 * j);
            }
            yield return null;
        }
    }

    sealed class PlanetInfo
    {
        public int PlanetSymbol;
        public GameObject Container, Image1, Image2;
        public float ContainerAngle, Angle1, Angle2;
        public float RotationSpeed;
        public bool Shrinking;
        public float Scale = 1;
    }

    private IEnumerator CreateAndRotatePlanet(int planetIx)
    {
        var cont = Instantiate(ContainerTemplate);
        cont.transform.parent = SwirlContainer.parent;
        cont.transform.localPosition = new Vector3(0, 0, -0.002f);
        cont.transform.localScale = new Vector3(_planetSize, _planetSize, _planetSize);
        cont.gameObject.SetActive(true);

        var img1 = Instantiate(ImageTemplate);
        img1.transform.parent = cont.transform;
        img1.transform.localPosition = new Vector3(-.1f, 0, 0);
        img1.transform.localScale = new Vector3(1, 1, 1);
        img1.gameObject.SetActive(true);
        img1.material.mainTexture = _planetTextures[planetIx + 1][_digitsEntered];
        img1.material.renderQueue = 2900;

        var img2 = Instantiate(ImageTemplate);
        img2.transform.parent = cont.transform;
        img2.transform.localPosition = new Vector3(.1f, 0, 0);
        img2.transform.localScale = new Vector3(1, 1, 1);
        img2.gameObject.SetActive(true);
        img2.material.mainTexture = _planetTextures[planetIx + 1][_digitsEntered];
        img2.material.renderQueue = 2900;

        var planet = _activePlanet = new PlanetInfo { Container = cont, Image1 = img1.gameObject, Image2 = img2.gameObject, PlanetSymbol = planetIx };

        planet.ContainerAngle = Rnd.Range(0, 360f);
        planet.Angle1 = Rnd.Range(0, 360f);
        planet.Angle2 = planet.Angle1 + 180;
        planet.RotationSpeed = Rnd.Range(25f, 40f);

        const float shrinkDuration = 1.5f;
        float shrinkElapsed = 0;

        while (planet.Container != null)
        {
            planet.Container.transform.localEulerAngles = new Vector3(0, 0, planet.ContainerAngle -= planet.RotationSpeed * Time.deltaTime);
            planet.Image1.transform.localEulerAngles = new Vector3(0, 0, planet.Angle1 -= 2 * planet.RotationSpeed * Time.deltaTime);
            if (planet.Image2 != null)
                planet.Image2.transform.localEulerAngles = new Vector3(0, 0, planet.Angle2 -= 2 * planet.RotationSpeed * Time.deltaTime);

            if (planet.Shrinking)
            {
                shrinkElapsed += Time.deltaTime;
                if (shrinkElapsed >= shrinkDuration)
                {
                    Destroy(planet.Container);
                    planet.Container = null;
                    yield break;
                }
                else
                {
                    var t = (1 - Mathf.Min(1, shrinkElapsed / shrinkDuration)) * _planetSize;
                    planet.Container.transform.localScale = new Vector3(t, t, t);
                }
            }

            yield return null;
        }
    }

    private enum Event
    {
        MouseUp,
        MouseDown,
        Tick
    }

    private List<Event> _events = new List<Event>();
    private int _lastTime = 0;
    private int _lastSolved = 0;

    private void HoleInteractEnded()
    {
        if (!_isSolved && _events.Count(e => e == Event.MouseDown) > _events.Count(e => e == Event.MouseUp))
        {
            _events.Add(Event.MouseUp);
            checkEvents();
        }
    }

    private bool HoleInteract()
    {
        if (!_isSolved)
        {
            if (_events.All(e => e == Event.Tick))
                Audio.PlaySoundAtTransform("BlackHoleInput2", Selectable.transform);
            _events.Add(Event.MouseDown);
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
                _events.Add(Event.Tick);
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

    private static readonly Event[][] _gestures = new[]
    {
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp, Event.Tick },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp, Event.Tick },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp, Event.MouseDown, Event.Tick, Event.MouseUp, Event.Tick },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.Tick, Event.MouseUp, Event.Tick },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.MouseDown, Event.MouseUp, Event.Tick }
    };

    private static readonly Event[][] _planetPrefixes = new[]
    {
        new[] { Event.Tick, Event.MouseDown },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp, Event.MouseDown },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp, Event.MouseDown, Event.Tick, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.Tick, Event.Tick, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick, Event.MouseDown },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick, Event.MouseDown, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.Tick, Event.MouseDown, Event.Tick, Event.MouseUp },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.MouseDown },
        new[] { Event.Tick, Event.MouseDown, Event.MouseUp, Event.MouseDown, Event.MouseUp },
    };

    private void checkEvents()
    {
        while (_events.Count >= 2 && _events[0] == Event.Tick && (_events[1] == Event.Tick || _events[1] == Event.MouseUp))
            _events.RemoveAt(1);

        var input = _gestures.IndexOf(list => list.SequenceEqual(_events));
        if (input != -1)
        {
            process(input);
            return;
        }

        var planet = _planetPrefixes.LastIndexOf(p => p.SequenceEqual(_events.Take(p.Length)));
        if (_activePlanet != null && planet != _activePlanet.PlanetSymbol)
        {
            _activePlanet.PlanetSymbol = planet;
            var tx = _planetTextures[(planet + 1 + _digitsEntered) % _planetTextures.Length][_digitsEntered];
            _activePlanet.Image1.GetComponent<MeshRenderer>().material.mainTexture = tx;
            _activePlanet.Image2.GetComponent<MeshRenderer>().material.mainTexture = tx;
        }
        else if (_activePlanet == null && planet != -1)
            StartCoroutine(CreateAndRotatePlanet(planet));

        if (_events.Count(e => e == Event.MouseUp) >= _events.Count(e => e == Event.MouseDown))
        {
            var validPrefix = _gestures.IndexOf(list => list.Take(_events.Count).SequenceEqual(_events));
            if (validPrefix == -1)
            {
                Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is not a valid digit.", _moduleId, _events.JoinString(", "));
                Module.HandleStrike();
                _events.Clear();
                _events.Add(Event.Tick);
                destroyPlanet();
            }
        }
    }

    private void destroyPlanet()
    {
        if (_activePlanet != null)
        {
            Destroy(_activePlanet.Image1);
            if (_activePlanet.Image2 != null)
                Destroy(_activePlanet.Image2);
            Destroy(_activePlanet.Container);
            _activePlanet.Container = null;
            _activePlanet = null;
        }
    }

    private void process(int digit)
    {
        if (digit == 5)  // the “get count of correct digits” gesture
        {
            Debug.LogFormat(@"[Black Hole #{0}] You asked for the number of correctly entered digits and I’ll give it to you ({1}).", _moduleId, _info.DigitsEntered);
            showNumber(_info.DigitsEntered, white: true);
            Audio.PlaySoundAtTransform("BlackHoleSuckShort", Selectable.transform);
        }
        else if (digit != _info.SolutionCode[_info.DigitsEntered])
        {
            Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is a wrong digit (I expected {2}).", _moduleId, digit, _info.SolutionCode[_info.DigitsEntered]);
            Module.HandleStrike();
            destroyPlanet();
        }
        else
        {
            Debug.LogFormat(@"[Black Hole #{0}] You entered {1}, which is correct.", _moduleId, digit);
            _info.DigitsEntered++;
            _info.LastDigitEntered = this;
            showNumber(digit);
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
        _events.Clear();
        _events.Add(Event.Tick);
    }

    private void showNumber(int digit, bool white = false)
    {
        if (_activePlanet != null)
        {
            Destroy(_activePlanet.Image1);
            Destroy(_activePlanet.Image2);
            var tm = Instantiate(TextTemplate);
            tm.text = digit.ToString();
            tm.color = white ? Color.white : _colors[_digitsEntered];
            tm.transform.parent = _activePlanet.Container.transform;
            tm.transform.localPosition = new Vector3(0, 0, 0);
            tm.transform.localRotation = Quaternion.identity;
            tm.transform.localScale = new Vector3(.07f / _planetSize, .125f / _planetSize, .125f / _planetSize);
            tm.gameObject.SetActive(true);
            _activePlanet.Image1 = tm.gameObject;
            _activePlanet.Image2 = null;
            _activePlanet.Shrinking = true;
            _activePlanet = null;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Specify when to hold, release, tap, or wait for a timer tick in the correct order, for example: “!{0} hold, tick, release” or “!{0} tap, tick, tap”.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (_isSolved)
            yield break;

        var actions = new List<object>() { null, Tick(), new WaitForSeconds(.1f) };
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
                    actions.Add(Tick());
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

    private Func<object> Tick()
    {
        return () =>
        {
            var time = (int) Bomb.GetTime();
            return new WaitUntil(() => (int) Bomb.GetTime() != time);
        };
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        while (_digitsEntered < _digitsExpected)
        {
            StartCoroutine(CreateAndRotatePlanet(Rnd.Range(0, _planetTextures.Length - 1)));
            foreach (var obj in WaitWithTrue(Rnd.Range(.1f, .9f)))
                yield return obj;
            var planet = _activePlanet;
            process(_info.SolutionCode[_info.DigitsEntered]);
            while (planet.Container != null)
                yield return true;
        }
    }

    IEnumerable WaitWithTrue(float time)
    {
        var startTime = Time.time;
        while (Time.time < startTime + time)
            yield return true;
    }
}