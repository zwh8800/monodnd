# MonoGame vs Unity：常见 AI 陷阱

> **用途**: AI 代码生成时最常见的 MonoGame/Unity API 混淆
> **核心原则**: 本项目使用 MonoGame + 自定义 ECS，不使用 Unity 的任何 API

---

## 1. 游戏入口类

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `MonoBehaviour` | `Game` | MonoGame 继承 `Game` 类，不继承 `MonoBehaviour` |
| `new MonoBehaviour()` | `new Game1()` | Game 在 `Main()` 中手动实例化 |
| `Start()`, `Update()` | `Initialize()`, `Update(GameTime)` | 生命周期方法不同 |
| `Instantiate(prefab)` | `new Entity()` | 无预制体系统 |

### ❌ AI 常见错误：使用 MonoBehaviour

```csharp
// ❌ 错误 — Unity 写法
public class PlayerController : MonoBehaviour  // MonoBehaviour 不存在于 MonoGame！
{
    void Start() { }
    void Update() { }
}

// ✅ 正确 — MonoGame 写法
public class PlayerController
{
    public void Update(GameTime gameTime) { }
}
```

### ❌ AI 常见错误：使用 Unity 生命周期

```csharp
// ❌ 错误 — Unity 生命周期
void Awake() { }
void Start() { }
void OnEnable() { }

// ✅ 正确 — MonoGame 生命周期
protected override void Initialize() { }    // 构造后调用一次
protected override void LoadContent() { }   // Initialize 内调用
protected override void Update(GameTime gt) { }  // 每帧调用
protected override void Draw(GameTime gt) { }    // 每帧调用
```

---

## 2. 游戏对象管理

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `GameObject` | `Entity`（自定义） | 项目使用自定义 ECS，不是 Unity 的 GameObject |
| `GameObject.Find()` | 无 | MonoGame 无名称查找机制 |
| `transform.position` | `entity.Position`（自定义） | 自定义 Entity 类 |
| `AddComponent<T>()` | `entity.AddComponent<T>()`（自定义） | 用法类似但命名空间不同 |

### ❌ AI 常见错误：使用 GameObject / Transform

```csharp
// ❌ 错误 — Unity 写法
GameObject player = GameObject.Find("Player");
player.transform.position = new Vector3(x, y, z);

// ✅ 正确 — MonoGame + 自定义 ECS
Entity player = scene.GetEntityByName("Player");  // 自定义方法
player.Position = new Vector2(x, y);              // 自定义属性
```

---

## 3. 场景管理

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `SceneManager.LoadScene()` | `Core.StartSceneTransition()`（自定义） | 项目自定义场景管理 |
| `DontDestroyOnLoad()` | 无 | 需自行管理持久化对象 |
| `FindObjectOfType<T>()` | `ServiceLocator.Get<T>()`（自定义） | 使用 ServiceLocator |

### ❌ AI 常见错误：使用 Unity 场景 API

```csharp
// ❌ 错误 — Unity 写法
SceneManager.LoadScene("MainMenu");
DontDestroyOnLoad(gameObject);

// ✅ 正确 — MonoGame + 自定义 ECS
ServiceLocator.Get<IGameStateManager>().TransitionTo(SceneId.MainMenu);
```

---

## 4. 渲染管线

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Camera` | 无 `Camera` 类 | MonoGame 无内置相机，需用 `Matrix` 实现视图变换 |
| `Camera.main` | `Matrix.CreateTranslation()` | 视口变换通过 SpriteBatch 的 transformMatrix |
| `SpriteRenderer` | `SpriteBatch` | SpriteBatch 是绘图工具，不是组件 |
| `Material` | `Effect` | MonoGame 使用 Effect 着色器 |
| `Shader` 文件 (`.shader`) | 效果文件 (`.fx`) | HLSL 语法不同 |

### ❌ AI 常见错误：使用 Camera / SpriteRenderer

```csharp
// ❌ 错误 — Unity 写法
Camera.main.transform.position = new Vector3(x, y, -10);
GetComponent<SpriteRenderer>().sprite = mySprite;

// ✅ 正确 — MonoGame 写法
// 相机通过 SpriteBatch 变换矩阵实现
_spriteBatch.Begin(
    transformMatrix: Matrix.CreateTranslation(-cameraPosition.X, -cameraPosition.Y, 0)
);
_spriteBatch.Draw(texture, position, Color.White);
_spriteBatch.End();
```

---

## 5. 输入系统

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Input.GetKeyDown()` | `Keyboard.GetState().IsKeyDown()` | 注意边缘检测需要手动保存上一帧状态 |
| `Input.GetAxis()` | 无 | MonoGame 需要手动实现轴输入 |
| `Input.mousePosition` | `Mouse.GetState().X / Y` | 鼠标位置通过 MouseState |
| `Input.GetButton()` | `GamePad.GetState()` | 手柄输入 API |

### ❌ AI 常见错误：使用 Unity Input API

```csharp
// ❌ 错误 — Unity 写法
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ 正确 — MonoGame 写法
KeyboardState state = Keyboard.GetState();
if (state.IsKeyDown(Keys.Space))
{
    // 注：GetKeyDown 需要手动比较上一帧状态
}
```

---

## 6. 资源加载

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Resources.Load<T>()` | `Content.Load<T>()` | `assetName` 不包含路径扩展名 |
| `AssetBundle` | 无 | MonoGame 无资源包系统 |
| `Addressables` | 无 | 无地址寻址系统 |
| `Instantiate(prefab)` | `Content.Load<Texture2D>()` | 无预制体概念 |

### ❌ AI 常见错误：使用 Resources / Instantiate

```csharp
// ❌ 错误 — Unity 写法
Texture2D tex = Resources.Load<Texture2D>("Textures/hero");
GameObject obj = Instantiate(prefab, position, rotation);

// ✅ 正确 — MonoGame 写法
Texture2D tex = Content.Load<Texture2D>("Textures/hero");  // 不含扩展名！

// 创建实体
Entity entity = new Entity();  // 自定义 ECS
entity.Position = position;
```

---

## 7. 时间系统

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Time.deltaTime` | `gameTime.ElapsedGameTime.TotalSeconds` | 作为 `GameTime` 参数传入 |
| `Time.time` | `gameTime.TotalGameTime.TotalSeconds` | 累计游戏时间 |
| `Time.fixedDeltaTime` | `game.TargetElapsedTime` | 固定步长间隔 |
| `Time.timeScale` | 无原生支持 | 需自行实现全局时间缩放 |

### ❌ AI 常见错误：使用 Time.deltaTime

```csharp
// ❌ 错误 — Unity 写法
float speed = 100f;
float step = speed * Time.deltaTime;

// ✅ 正确 — MonoGame 写法
protected override void Update(GameTime gameTime)
{
    float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
    float step = speed * deltaTime;
}
```

---

## 8. 数学和向量

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Vector3` | `Vector2`（2D 项目） | 2D 项目使用 `Vector2` |
| `Quaternion` | `float` (旋转角度) | 2D 旋转用弧度 `float` |
| `Mathf` | `MathHelper` | MonoGame 的 `MathHelper` 类 |
| `Mathf.Lerp()` | `MathHelper.Lerp()` | API 类似 |
| `Mathf.Clamp()` | `MathHelper.Clamp()` | API 类似 |
| `Mathf.Deg2Rad` | `MathHelper.ToRadians()` | 角度→弧度转换 |
| `transform.Translate()` | `Vector2.Add()` 或 `+=` | 位置直接计算 |

### ❌ AI 常见错误：使用 Unity 数学 API

```csharp
// ❌ 错误 — Unity 写法
Vector3 moveDir = new Vector3(1, 0, 0) * speed * Time.deltaTime;
float clamped = Mathf.Clamp(value, 0, 100);
float lerped = Mathf.Lerp(a, b, t);

// ✅ 正确 — MonoGame 写法
Vector2 moveDir = new Vector2(1, 0) * speed * deltaTime;
float clamped = MathHelper.Clamp(value, 0, 100);
float lerped = MathHelper.Lerp(a, b, t);
```

---

## 9. 碰撞检测

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Collider` / `Collider2D` | 无原生碰撞系统 | 需自行实现或使用 GoRogue |
| `Physics.Raycast()` | 无 | 需自行实现射线检测 |
| `OnCollisionEnter()` | `IEventBus.Publish()` | 项目使用事件总线广播碰撞事件 |
| `Rigidbody` | 无 | 手动管理物理/移动 |

### ❌ AI 常见错误：使用 Unity 物理 API

```csharp
// ❌ 错误 — Unity 写法
void OnCollisionEnter2D(Collision2D collision) { }
Physics2D.Raycast(origin, direction, distance);

// ✅ 正确 — MonoGame + 自定义 ECS + GoRogue
// 使用 GoRogue 的 A* / FOV 或自定义矩形碰撞检测
if (bounds.Intersects(otherBounds))
{
    EventBus.Publish(new CollisionEvent(entity, otherEntity));
}
```

---

## 10. 对象生命周期和销毁

| Unity | MonoGame | 说明 |
|-------|----------|------|
| `Destroy(obj)` | 无对应 API | 需手动移除或回收实体 |
| `DestroyImmediate()` | 无 | MonoGame 无立即销毁概念 |
| `OnDestroy()` | `UnloadContent()` | 只有 Content 级别的卸载 |
| `GarbageCollect()` | `IDisposable` 模式 | 需要实现 `IDisposable` |
| `Object.DontDestroyOnLoad()` | `ServiceLocator` | 全局服务通过 ServiceLocator 保持 |

### ❌ AI 常见错误：使用 Unity 销毁 API

```csharp
// ❌ 错误 — Unity 写法
Destroy(gameObject);
DestroyImmediate(component);

// ✅ 正确 — MonoGame + 自定义 ECS
scene.RemoveEntity(entity);           // 自定义移除
component.Dispose();                  // 显式释放资源
ServiceLocator.Get<IAudioManager>().Stop(soundEffect);  // 通过服务管理
```

---

## 快速对照表

| 功能 | Unity | MonoGame |
|------|-------|----------|
| 入口类 | `MonoBehaviour` | `Game` |
| 入口方法 | `Start()` / `Update()` | `Initialize()` / `Update(GameTime)` / `Draw(GameTime)` |
| 游戏对象 | `GameObject` | `Entity`（自定义 ECS） |
| 添加组件 | `AddComponent<T>()` | `entity.AddComponent<T>()`（自定义） |
| 2D 渲染 | `SpriteRenderer` + `Camera` | `SpriteBatch` + 变换矩阵 |
| 场景管理 | `SceneManager.LoadScene()` | `Core.StartSceneTransition()`（自定义） |
| 输入 | `Input.GetKey()` / `GetAxis()` | `Keyboard.GetState()` / `Mouse.GetState()` |
| 资源加载 | `Resources.Load<T>()` | `Content.Load<T>()` |
| 时间 | `Time.deltaTime` | `gameTime.ElapsedGameTime.TotalSeconds` |
| 数学 | `Mathf` | `MathHelper` |
| 向量 | `Vector3` | `Vector2`（2D） |
| 旋转 | `Quaternion` | `float`（弧度） |
| 碰撞 | `Collider` + `Physics` | 自定义 / GoRogue |
| 销毁 | `Destroy()` | `RemoveEntity()` / `IDisposable.Dispose()` |
| 服务查找 | `FindObjectOfType<T>()` | `ServiceLocator.Get<T>()` |
| 事件 | `UnityEvent` / `SendMessage` | `IEventBus.Publish<T>()`（自定义） |

---

## 关键词黑名单（AI 代码审查用）

以下 Unity API 关键词**绝对不应当出现在 MonoGame 项目中**：

```
MonoBehaviour    GameObject       Instantiate
transform        FindObjectOfType DontDestroyOnLoad
SceneManager     Resources.Load   Addressables
Collider         Rigidbody        Collider2D
OnCollisionEnter OnTriggerEnter   OnDestroy
Input.GetKey     Input.GetAxis    Input.mousePosition
Time.deltaTime   Time.time        Time.timeScale
Camera.main      Camera           SpriteRenderer
Material         Shader           Prefab
```

> **审查规则**: 如果 AI 生成的代码包含以上任何关键词，标记为编译错误并替换为对应的 MonoGame/自定义实现。
