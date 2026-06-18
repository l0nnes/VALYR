# VALYR Vehicle System - характеристики и логика проекта

Документ описывает текущую структуру и поведение кода в `Assets/Scripts`.
Цель - дать справочник по проекту, чтобы не искать базовые ответы в исходниках.

## 1. Назначение проекта

Проект реализует модульную физическую систему автомобиля для Unity.
Основная идея: отделить чистую автомобильную симуляцию от Unity API.

Система состоит из двух слоев:

- `Core` - ядро симуляции, состояние машины, модули физики, математика, интерфейсы хоста.
- `Unity` - Unity-интеграция: `MonoBehaviour`, `Rigidbody`, `Physics.Raycast`, `SweepTest`, `ScriptableObject`-конфиги, Input System, визуализация колес и debug GUI.

Ключевой класс ядра - `Core.VehicleCore`.
Ключевой Unity-компонент - `Unity.VehicleController`.

## 2. Общая архитектура папок

### `Core`

`Core` содержит код, который почти полностью не зависит от Unity.
Исключение: адаптеры `Core/UnityAdapters` включаются только под Unity через `#if UNITY_5_3_OR_NEWER`.

Основные файлы:

- `VehicleCore.cs` - главный цикл симуляции, сбор контекста, вызов модулей, применение сил.
- `VehicleConfig.cs` - конфигурация машины, осей и колес.
- `VehicleState.cs` - все runtime-состояния: двигатель, коробка, колеса, шины, подвеска, ввод.
- `VehicleModulePhase.cs` - порядок фаз модулей.
- `Contexts/Contexts.cs` - `VehicleContext`, `AxleContext`, `WheelContext`.
- `Interfaces/IVehicleModule.cs` - контракт модулей.
- `Interfaces/IVehicleHost.cs` - контракт внешнего физического мира.
- `Modules/*` - конкретные модули контакта, подвески, рулевого, двигателя, трансмиссии, шин и ассистов.
- `Math/*` - собственные `Vec3`, `Quat`, `MathUtil`, кривые двигателя и шин.
- `Configs/ModuleConfig.cs` - базовый класс конфигов модулей.
- `Attributes/Attributes.cs` - атрибуты для отображения конфигов.

### `Unity`

`Unity` содержит связь ядра с Unity.

Основные файлы:

- `VehicleController.cs` - Unity-компонент машины, создает `VehicleCore`, читает ввод, запускает симуляцию, синхронизирует визуальные колеса.
- `UnityVehicleHost.cs` - реализация `IVehicleHost` поверх `Rigidbody`, `Physics.Raycast` и sweep-коллайдера колеса.
- `VehicleDebugger.cs` - телеметрия через IMGUI, включается клавишей F3.
- `Data/VehiclePreset.cs` - `ScriptableObject` пресет машины, конвертируется в `VehicleConfig`.
- `Input/GameInput.cs` - сгенерированный класс Unity Input System.
- `GeneratedConfigs/*` - `ScriptableObject`-обертки для конфигов модулей.
- `Editor/EnginePowerCurveDrawer.cs` - редакторский график мощности и момента двигателя.
- `Test/RotatingPlatform.cs` - тестовая движущаяся платформа.

## 3. Главные сущности

### `VehicleCore`

`VehicleCore` - основной объект симуляции.

Он:

- хранит `VehicleContext`;
- принимает `VehicleConfig`;
- сортирует модули по фазам;
- делит модули на макро-модули и микро-модули;
- строит оси и колеса по `hardPoints`;
- каждый `FixedUpdate` обновляет ввод, состояние кузова, модули и применяет силы.

Разделение модулей:

- macro modules - все модули, кроме `VehicleModulePhase.Tire`;
- micro modules - модули фазы `Tire`.

Причина: шины считаются в substeps, потому что силы шин чувствительны к маленькому шагу интеграции.

### `VehicleContext`

`VehicleContext` - общий runtime-контекст симуляции.

Содержит:

- `config` - конфигурация машины;
- `host` - внешний мир через `IVehicleHost`;
- `body` - снимок состояния `Rigidbody`;
- `input` - ввод на текущий физический кадр;
- `engine` - состояние двигателя;
- `clutch` - состояние сцепления;
- `transmission` - состояние коробки;
- `axles` - список осей;
- `wheels` - плоский список колес.

`RecalculateBody()` читает из хоста:

- позицию;
- вращение;
- линейную скорость;
- угловую скорость.

### `AxleContext`

Описывает одну ось автомобиля.

Содержит:

- ссылку на `VehicleContext`;
- индекс оси;
- `AxleConfig`;
- `AxleState`;
- левое колесо;
- правое колесо.

### `WheelContext`

Описывает одно колесо.

Содержит:

- ссылку на ось;
- индекс колеса в общем списке;
- признак левой стороны;
- `WheelConfig`;
- `WheelState`.

Индексация колес в `VehicleCore.BuildContext()`:

- ось `i`;
- левое колесо - индекс `i * 2`;
- правое колесо - индекс `i * 2 + 1`.

## 4. Конфигурация машины

### `VehicleConfig`

Содержит:

- `subSteps` - количество микрошагов для шин, по умолчанию 4;
- `mass` - масса кузова;
- `axles` - массив осей;
- `modules` - массив модулей `IVehicleModule`.

### `AxleConfig`

Содержит:

- `isPowered` - ось получает приводной момент от трансмиссии;
- `steeringMode` - режим руления;
- `wheel` - конфиг колес на этой оси.

### `SteeringMode`

Режимы:

- `Disable` - ось не рулит;
- `Standard` - обычное направление руля;
- `Reverse` - обратное направление, полезно для заднего подруливания.

### `WheelConfig`

Содержит:

- `radius` - радиус колеса;
- `width` - ширина колеса;
- `mass` - масса колеса;
- `inertia` - момент инерции колеса.

В `VehiclePreset.AxleSetup.ToCore()` инерция считается как:

```text
inertia = mass * radius * radius
```

Если параметры оси не заданы, используются fallback-значения:

- radius: `0.35`;
- width: `0.25`;
- mass: `15`.

## 5. Runtime-состояния

### `InputState`

Текущий ввод:

- `throttle` - газ, 0..1;
- `steering` - руль, обычно -1..1;
- `brake` - тормоз, 0..1;
- `isUpshift` - запрос передачи вверх;
- `isDownshift` - запрос передачи вниз;
- `handbrake` - ручник;
- `clutch` - ручное сцепление, 0..1.

Важно: в логике трансмиссии `input.clutch = 1` означает выжатую педаль, а `clutch.engagement = 1` означает сомкнутое сцепление.

### `RigidbodyState`

Снимок физического тела:

- `position`;
- `rotation`;
- `linearVelocity`;
- `angularVelocity`.

### `EngineState`

Состояние двигателя:

- `rpm` - обороты для UI и логики;
- `angularVelocity` - угловая скорость маховика, rad/s;
- `torque` - текущий момент сгорания;
- `loadTorque` - нагрузка от трансмиссии;
- `inertia` - инерция двигателя;
- `inCutoff` - активна отсечка;
- `crankTimer` - таймер стартера;
- `isRunning` - двигатель заведен.

### `ClutchState`

Содержит:

- `engagement` - степень замыкания сцепления: `0` разомкнуто, `1` сомкнуто.

### `TransmissionState`

Содержит:

- `currentGear` - индекс текущей передачи;
- `currentGearRatio` - итоговое передаточное число с главной парой;
- `torque` - момент, проходящий через коробку.

Индексы передач по умолчанию:

- `0` - reverse;
- `1` - neutral;
- `2` и выше - передние передачи, где отображаемая передача равна `currentGear - 1`.

### `WheelState`

Геометрия и динамика колеса:

- `localHardPoint` - локальная точка крепления подвески;
- `worldPosition` - мировая позиция hard point;
- `worldRotation` - мировое вращение колеса с рулевым углом;
- `forwardDir`, `rightDir`, `upDir` - оси колеса;
- `driveTorque` - приводной момент;
- `brakeTorque` - тормозной момент;
- `steeringAngle` - текущий угол поворота колеса;
- `hit` - контакт с землей;
- `suspension` - состояние подвески;
- `tire` - состояние шины.

### `GroundHitState`

Данные контакта:

- `isGrounded`;
- `point`;
- `normal`;
- `distance`;
- `surfaceVelocity`.

`surfaceVelocity` нужен для движущихся поверхностей: например, вращающихся или движущихся платформ.

### `SuspensionState`

Состояние подвески:

- `compressionRatio` - сжатие 0..1;
- `currentLength` - текущая длина подвески;
- `force` - вертикальная сила подвески.

### `TireState`

Состояние шины:

- `longForce` - продольная сила;
- `latForce` - боковая сила;
- `totalForce` - итоговая мировая сила;
- `angularVelocity` - угловая скорость колеса;
- `angularAcceleration` - угловое ускорение;
- `slipAngle` - угол бокового скольжения, градусы;
- `slipRatio` - продольное скольжение.

## 6. Контракт хоста `IVehicleHost`

Ядро не применяет силы напрямую к Unity. Оно обращается к `IVehicleHost`.

Хост обязан предоставить:

- `DeltaTime`;
- `GetInput()`;
- `GetPosition()`;
- `GetRotation()`;
- `GetLinearVelocity()`;
- `GetAngularVelocity()`;
- `GetPointVelocity(worldPoint)`;
- `GetLocalPointVelocity(worldPoint)`;
- `Raycast(origin, direction, maxDistance, out result)`;
- `SweepWheel(...)`;
- `ApplyForce(force, position)`.

`RaycastResult` содержит:

- `Point`;
- `Normal`;
- `Distance`;
- `SurfaceVelocity`.

В Unity реализация находится в `UnityVehicleHost`.

## 7. Жизненный цикл создания машины

### Шаг 1. Unity вызывает `VehicleController.Awake()`

`VehicleController`:

1. Создает `GameInput`.
2. Получает `Rigidbody`.
3. Проверяет, что назначен `VehiclePreset`.
4. Вызывает `vehicleConfigAsset.CreateConfig()`.
5. Проверяет, что количество осей в конфиге равно количеству `axleTransforms` в сцене.
6. Создает `UnityVehicleHost`.
7. Создает `VehicleCore`.
8. Собирает `hardPoints` из `leftHardPoint.localPosition` и `rightHardPoint.localPosition`.
9. Вызывает `Core.Initialize(_host, hardPoints.ToArray())`.
10. Создает массив `_visualWheelRotation`.
11. Устанавливает `_rb.mass = coreConfig.mass`.

### Шаг 2. `VehicleCore.Initialize()`

`VehicleCore`:

1. Запоминает `host` в контексте.
2. Вызывает `BuildContext(hardPoints)`.
3. Инициализирует macro modules.
4. Инициализирует micro modules.

### Шаг 3. `BuildContext()`

Для каждой оси в `config.axles` создается:

- `AxleContext`;
- левый `WheelContext`;
- правый `WheelContext`.

Колеса добавляются в:

- `axle.leftWheel`;
- `axle.rightWheel`;
- общий список `Context.wheels`.

## 8. Жизненный цикл кадра

### `VehicleController.Update()`

Каждый обычный кадр:

1. Передает накопленный ввод в `_host.SetInput(...)`.
2. Сбрасывает одноразовые флаги переключения передач `_isUpshift` и `_isDownshift`.
3. Обновляет визуальные колеса через `SyncVisuals()`.

### `VehicleController.FixedUpdate()`

Каждый физический кадр:

```csharp
Core?.Simulate();
```

### `VehicleCore.Simulate()`

Порядок:

1. Если `Context.host == null`, выйти.
2. Взять `dt = Context.host.DeltaTime`.
3. Взять `subSteps = Context.config.subSteps`.
4. Посчитать `subDt = dt / subSteps`.
5. Прочитать ввод: `Context.input = Context.host.GetInput()`.
6. Пересчитать состояние кузова: `Context.RecalculateBody()`.
7. Выполнить все macro modules один раз за физический кадр.
8. Выполнить `subSteps` циклов:
   - обновить все micro modules с `subDt`;
   - применить силы шин с масштабом `1 / subSteps`.
9. Применить силы подвески.

Важно: силы шин применяются внутри substeps, силы подвески - после них один раз за кадр.

## 9. Фазы модулей

`VehicleModulePhase` задает порядок:

| Phase | Значение | Назначение |
|---|---:|---|
| `InputModifiers` | 0 | ABS, ESP, TCS, модификация ввода и тормозов |
| `GroundDetection` | 10 | поиск контакта колес с поверхностью |
| `Suspension` | 20 | расчет пружин и амортизаторов |
| `AntiRollBar` | 25 | стабилизаторы, фаза зарезервирована |
| `Steering` | 30 | рулевая логика |
| `Engine` | 40 | двигатель |
| `Transmission` | 50 | сцепление и КПП |
| `Differential` | 60 | дифференциал, фаза зарезервирована |
| `Tire` | 70 | расчет сил шин, выполняется в substeps |

Модули сортируются по `Phase` при создании `VehicleCore`.

Особенность текущей логики: `DrivingAssistsModule` находится в фазе `InputModifiers`, то есть выполняется до `TireModule`. При этом он читает slip-данные из предыдущего расчета шин и на их основе меняет текущий throttle/brake. Это нормально для feedback-ассистов с задержкой в один физический кадр.

## 10. Применение сил

### Силы шин

`ApplyTireForces(scale)`:

- проходит по колесам;
- пропускает не grounded колеса;
- берет `wheel.state.tire.totalForce`;
- применяет силу в точке контакта `wheel.state.hit.point`;
- умножает силу на `scale`, где `scale = 1 / subSteps`.

Итого за один физический кадр сумма substep-сил соответствует полной силе.

### Силы подвески

`ApplySuspensionForces()`:

- проходит по колесам;
- пропускает не grounded колеса;
- берет `wheel.state.suspension.force * wheel.state.upDir`;
- применяет силу в `wheel.state.worldPosition`.

Точка приложения подвески - hard point колеса, а не точка контакта.

## 11. Модули контакта с поверхностью

Все модули контакта имеют фазу `GroundDetection`.

Они обновляют:

- `wheel.state.worldPosition`;
- `wheel.state.upDir`;
- `wheel.state.worldRotation`;
- `wheel.state.forwardDir`;
- `wheel.state.rightDir`;
- `wheel.state.hit`.

Во всех модулях steering rotation задается как:

```text
worldRotation = bodyRotation * Quat.Euler(0, steeringAngle, 0)
```

Текущее устройство направлений колеса:

```text
upDir = bodyRotation * Vec3.Up
forwardDir = worldRotation * Vec3.Forward
rightDir = worldRotation * Vec3.Right
```

Важно: в текущем коде `forwardDir` и `rightDir` не проецируются на плоскость контакта через `hit.normal`. Нормаль контакта сохраняется в `wheel.state.hit.normal`, но `TireModule` считает `vLong`, `vLat` и `totalForce` по направлениям, полученным от ориентации кузова/руля. Метод `Vec3.ProjectOnPlane` в математике есть, но в расчетах направлений колес сейчас не используется.

### `RaycastModule`

Самый простой вариант контакта.

Логика:

1. Луч стартует из `wheel.state.worldPosition`.
2. Направление - вниз по оси кузова: `-wheel.state.upDir`.
3. Длина луча: `wheel.radius + config.castDistance`.
4. При попадании пишет точку, нормаль, расстояние и скорость поверхности.
5. При промахе выставляет `isGrounded = false`, точку внизу на scan distance, нормаль вверх, surface velocity zero.

Конфиг:

- `castDistance = 0.2`.

Подходит для простого и дешевого контакта, но плохо описывает ширину колеса и боковые/радиальные касания.

### `WideRaycastModule`

Вариант с несколькими лучами по ширине колеса.

Логика:

1. Количество лучей: `max(2, rayCount)`.
2. Эффективная ширина: `wheel.width * widthFactor`.
3. Лучи распределяются от `-effectiveWidth / 2` до `+effectiveWidth / 2` вдоль `rightDir`.
4. Каждый луч идет вниз.
5. Выбирается попадание с минимальной дистанцией.

Конфиг:

- `castDistance = 0.5`;
- `rayCount = 3`;
- `widthFactor = 0.9`.

Лучше одиночного луча на неровностях и краях, но все еще не учитывает форму окружности колеса.

### `CylindricalRaycastModule`

Сканирует сектор цилиндра набором лучей по радиусу и ширине.

Логика:

1. `maxRange = wheel.radius + castDistance`.
2. По ширине используется `widthResolution`.
3. По радиальному сектору используется `radialResolution`.
4. Сектор задается `sectorAngle` в градусах, центрирован вниз.
5. Направление луча строится из смеси down axis и forward axis:

```text
rayDir = normalize(downAxis * cos(angle) + forwardAxis * sin(angle))
```

6. Из всех попаданий выбирается минимальная дистанция.

Конфиг:

- `castDistance = 0.2`;
- `sectorAngle = 160`;
- `radialResolution = 7`;
- `widthResolution = 3`;
- `widthFactor = 0.9`.

Более реалистично ловит контакт в нижнем секторе колеса. Стоимость выше, потому что количество raycast равняется `widthResolution * radialResolution` на колесо.

### `MeshSweepModule`

Использует sweep колеса через `IVehicleHost.SweepWheel`.

Логика:

1. Берет радиус и ширину колеса.
2. Старт - `wheel.state.worldPosition`.
3. Направление - `-wheel.state.upDir`.
4. Дистанция sweep - `config.castDistance`.
5. Передает `wheel.index`, позицию, вращение, направление, дистанцию, радиус и ширину в хост.

Конфиг:

- `castDistance = 0.5`.

В Unity хост создает скрытый convex `MeshCollider` в форме цилиндрического колеса и вызывает `Rigidbody.SweepTest`.

Особенность Unity-реализации:

- для каждого wheel index создается отдельный `WheelSweeper`;
- collider скрыт через `HideFlags.HideAndDontSave`;
- коллайдер sweeper игнорирует коллайдеры машины;
- mesh пересоздается только если изменились radius или width;
- при попадании `Distance = hit.distance + radius`.

## 12. `SuspensionModule`

Фаза: `Suspension`.

Задача: посчитать вертикальную силу подвески для каждого grounded колеса.

### Если колесо не на земле

Сбрасывается:

- `compressionRatio = 0`;
- `currentLength = restLength`;
- `force = 0`;
- внутренние `previousCompression` и `velocity`.

### Если колесо на земле

Текущая длина:

```text
currentLength = hit.distance - wheel.radius
currentLength = clamp(currentLength, 0.001, restLength)
```

Сжатие:

```text
compression = 1 - currentLength / restLength
compression = clamp01(compression)
```

Сила пружины:

```text
springForce = (restLength - currentLength) * springStiffness
```

Bump stop:

```text
if compression > bumpStopThreshold:
    overCompression = compression - bumpStopThreshold
    bumpForce = overCompression * restLength * springStiffness * bumpStopMultiplier
    springForce += bumpForce
```

Скорость хода подвески:

```text
previousLength = (1 - previousCompression) * restLength
rawVelocity = (previousLength - currentLength) / dt
velocity = lerp(previousVelocity, rawVelocity, 1 - velocitySmoothing)
```

Демпфер:

```text
if velocity >= 0:
    damperForce = velocity * bumpDamper
else:
    damperForce = velocity * reboundDamper
```

Итог:

```text
force = max(0, springForce + damperForce)
```

Конфиг:

- `restLength = 0.5`;
- `springStiffness = 30000`;
- `damperStiffness = 4000` - поле есть, но в текущей логике не используется;
- `reboundDamper = 4500`;
- `bumpDamper = 3000`;
- `bumpStopThreshold = 0.8`;
- `bumpStopMultiplier = 5.0`;
- `velocitySmoothing = 0.5`.

## 13. `SteeringModule`

Фаза: `Steering`.

Задача: посчитать углы руления осей и колес.

### Инициализация

Если осей меньше 2, Ackermann не инициализируется.

Для Ackermann считаются:

- wheel base - расстояние по Z между первой и последней осью;
- track width - расстояние по X между левым и правым колесом первой оси.

### Обновление

Для каждой оси:

1. Если `steeringMode == Disable`, ось пропускается.
2. Целевой угол: `input.steering * maxSteeringAngle`.
3. Если `steeringMode == Reverse`, знак угла инвертируется.
4. Выбирается скорость движения руля:
   - `steerSpeed`, если угол уходит дальше в ту же сторону или стартует из нуля;
   - `recenteringSpeed`, если руль возвращается.
5. `axle.state.steerAngle` двигается к target через `MoveTowards`.
6. Угол применяется к колесам с учетом Ackermann.

### Ackermann

Если `ackermannFactor` почти ноль, модуль не инициализирован или угол меньше `0.1`, оба колеса получают один и тот же угол.

Иначе:

```text
turnRadius = wheelBase / tan(abs(angle))
```

Для правого поворота внутренним считается правое колесо, для левого - левое.

Итоговые углы смешиваются:

```text
wheelAngle = lerp(baseAngle, ackermannAngle, ackermannFactor)
```

Конфиг:

- `maxSteeringAngle = 35`;
- `ackermannFactor = 0.5`;
- `steerSpeed = 100`;
- `recenteringSpeed = 200`.

## 14. `DrivingAssistsModule`

Фаза: `InputModifiers`.

Задача: электронные ассисты ABS, TCS, ESP.

Модуль меняет:

- `context.input.throttle`;
- `wheel.state.brakeTorque`.

Важно: ассисты читают `slipRatio` и `slipAngle`, которые были посчитаны `TireModule` на предыдущем физическом кадре.

### Sensor pass

Собирает:

- максимальный боковой slip angle среди grounded колес;
- максимальный forward slip ratio среди powered колес.

### ESP

Если включен ESP и газ больше `0.01`:

```text
if maxLatSlip > espSlipAngleThreshold:
    severity = (maxLatSlip - threshold) * espThrottleSensitivity
    throttle *= clamp01(1 - severity)
```

Цель - уменьшить газ при сильном боковом скольжении.

### TCS throttle cut

Если включен TCS и газ больше `0.01`:

```text
if maxForwardSlip > tcsSlipThreshold:
    severity = (maxForwardSlip - threshold) * tcsThrottleSensitivity
    throttle *= clamp01(1 - severity)
```

Цель - уменьшить пробуксовку ведущих колес.

### ABS

Базовый тормоз:

```text
baseBrakeTorque = input.brake * maxBrakeForce
```

Если ABS включен, тормоз есть и колесо grounded:

```text
if slipRatio < -absSlipThreshold:
    severity = (-slipRatio - threshold) * absSensitivity
    appliedBrakeTorque *= clamp01(1 - severity)
```

Отрицательный slip означает, что колесо вращается медленнее дороги и может блокироваться.

### TCS brake

Если TCS включен, колесо ведущее, grounded и газ есть:

```text
if slipRatio > tcsBrakeSlipThreshold:
    severity = (slipRatio - threshold) * tcsBrakeSensitivity
    tcsBrakeClamp = maxBrakeForce * tcsMaxBrakeForcePercent
    tcsBrakeApplied = clamp(severity * maxBrakeForce, 0, tcsBrakeClamp)
    appliedBrakeTorque = max(appliedBrakeTorque, tcsBrakeApplied)
```

Цель - притормозить буксующее ведущее колесо.

Конфиг:

- `maxBrakeForce = 4000`;
- `enableABS = true`;
- `absSlipThreshold = 0.15`;
- `absSensitivity = 8`;
- `enableTCS = true`;
- `tcsSlipThreshold = 0.15`;
- `tcsThrottleSensitivity = 5`;
- `tcsBrakeSlipThreshold = 0.25`;
- `tcsBrakeSensitivity = 3`;
- `tcsMaxBrakeForcePercent = 0.4`;
- `enableESP = true`;
- `espSlipAngleThreshold = 12`;
- `espThrottleSensitivity = 0.1`.

## 15. `EnginePowerCurve`

Кривая двигателя. Хранит точки `rpm/torqueNm`, плавно интерполирует момент и переводит его в мощность:

```text
powerKw = torqueNm * rpm / 9549.296
horsePower = powerKw * 1.341022
```

В Unity Editor для этой кривой есть `EnginePowerCurveDrawer`, который рисует torque и power с разными шкалами и маркерами точек.

## 16. `EngineModule`

Фаза: `Engine`.

Задача: semicade-симуляция двигателя через RPM, момент, трение, engine braking, idle control и отсечку.

В текущей версии двигатель всегда включен:

- `engine.isRunning = true`;
- стартер и ключ зажигания не используются;
- нагрузка от КПП может просадить обороты до idle, но не может заглушить двигатель.

Основной поток:

```text
availableTorque = curve.EvaluateTorqueNm(rpm)
combustionTorque = availableTorque * throttle
netTorque = combustionTorque - frictionTorque - engine.loadTorque
angularVelocity += (netTorque / inertia) * dt
angularVelocity = clamp(angularVelocity, idleOmega, maxOmega)
```

## 17. `GearboxModule`

Фаза: `Transmission`.

Задача: ручная или автоматическая коробка передач, сцепление и расчет выходного момента коробки.

Индексы передач:

- `0` - reverse;
- `1` - neutral;
- `2+` - передние передачи, отображаемая передача равна `currentGear - 1`.

Модуль пишет:

- `transmission.currentGear`;
- `transmission.currentGearRatio`;
- `transmission.outputTorque`;
- `transmission.inputShaftRpm`;
- `clutch.engagement`;
- `engine.loadTorque`.

Для автоматического режима есть racing-game stop controls:

- если машина полностью остановлена и тормоз удерживается дольше `brakeToReverseDelay`, включается reverse;
- в reverse педаль тормоза работает как газ назад, а обычный газ тормозит задний ход и при остановке возвращает первую передачу;
- если тормоз отпущен после полной остановки до включения reverse, включается `auto hold`;
- `auto hold` держит тормозной момент до небольшого нажатия газа.

## 18. `DrivetrainModule`

Фаза: `Differential`.

Задача: распределение `transmission.outputTorque` по колесам.

Поддерживаются схемы:

- `FWD`;
- `RWD`;
- `AWD`;
- `CustomAxles`.

Поддерживаются режимы блокировок:

- `Open`;
- `LimitedSlip`;
- `Locked`.

## 19. `TireModule`

Фаза: `Tire`.

Задача: рассчитать signed longitudinal/lateral slip, combined-slip силы шины, итоговую силу в плоскости контакта и угловую скорость колеса.

Актуальная модель после сверки со Street-Spec:

- `SoftWheelContactModule` задает contact patch, усредненную normal, `penetrationSum` и `normalForce`;
- `TireModule` строит `contactForward/contactRight` в плоскости контакта, а не по плоскости кузова;
- longitudinal force зависит от lateral slip через `longitudinalFree` -> `longitudinalAtMaxLateral`;
- lateral force зависит от longitudinal slip через `lateralFree` -> `lateralAtMaxLongitudinal`;
- friction circle используется только как safety limiter через `safetyGripMultiplier`;
- brake torque участвует в longitudinal force demand до интеграции `tire.angularVelocity`.

Этот модуль выполняется в micro substeps.

### Если колесо в воздухе

Сбрасываются:

- `longForce = 0`;
- `latForce = 0`;
- `totalForce = Vec3.Zero`;
- `slipRatio = 0`;
- `slipAngle = 0`.

Затем все равно применяются torque effects к угловой скорости колеса:

- drive torque раскручивает колесо;
- brake torque тормозит;
- если нет drive/brake, угловая скорость плавно затухает.

### Ручник

Если `context.input.handbrake == true` и `wheel.axle.index > 0`, тормозной момент становится не меньше `handbrakeTorque`.

То есть ручник применяется ко всем осям кроме оси 0.

### Скорости контакта

```text
hubVel = host.GetPointVelocity(wheel.worldPosition) - hit.surfaceVelocity
vLong = dot(hubVel, forwardDir)
vLat = dot(hubVel, rightDir)
vWheel = tire.angularVelocity * wheel.radius
```

`surfaceVelocity` позволяет корректно ездить по движущимся объектам.

### Slip ratio

```text
denominator = max(abs(vWheel), abs(vLong), 1.0)
slipRatio = (vWheel - vLong) / denominator
```

Положительный slip - колесо крутится быстрее движения машины.
Отрицательный slip - колесо вращается медленнее дороги, характерно для торможения/блокировки.

### Slip angle

```text
slipAngleRad = atan2(vLat, max(abs(vLong), 1.0))
slipAngle = slipAngleRad * Rad2Deg
```

### Коэффициенты сцепления

```text
muLong = friction.EvaluateLongitudinal(slipRatio, slipAngle)
muLat = friction.EvaluateLateral(slipAngle, slipRatio)
load = max(hit.normalForce, suspension.force)
```

Если load меньше `0.1`, load становится 0.

### Сырые силы

```text
rawLongForce = sign(slipRatio) * muLong * load
rawLatForce = sign(-vLat) * muLat * load
```

### Ограничение продольной силы синхронизацией

Без тормоза модуль не дает продольной силе быть больше силы, которая нужна для синхронизации скорости колеса и дороги:

```text
targetOmega = vLong / radius
deltaOmega = angularVelocity - targetOmega
torqueToSync = deltaOmega * inertia / dt
fSyncLong = (torqueToSync + driveTorque) / radius
```

Если `rawLongForce` больше `fSyncLong` по модулю и того же знака, `rawLongForce` ограничивается `fSyncLong`.

### Ограничение боковой силы синхронизацией

```text
massPerWheel = load / 9.81
fSyncLat = massPerWheel * -vLat / dt
```

Если `rawLatForce` больше `fSyncLat` по модулю и того же знака, она ограничивается `fSyncLat`.

### Safety limit

```text
maxGrip = load * safetyGripMultiplier
totalForceMag = sqrt(rawLongForce^2 + rawLatForce^2)
if totalForceMag > maxGrip:
    scale = maxGrip / totalForceMag
    rawLongForce *= scale
    rawLatForce *= scale
```

### Итоговая сила

```text
totalForce = contactForward * rawLongForce + contactRight * rawLatForce
```

### Угловая скорость колеса

```text
frictionTorque = fLong * radius
netTorque = driveTorque - frictionTorque
angularAcceleration = netTorque / inertia
angularVelocity += angularAcceleration * dt
```

Тормоз:

```text
brakeDecay = brakeTorque / inertia * dt
if abs(angularVelocity) <= brakeDecay:
    angularVelocity = 0
else:
    angularVelocity -= sign(angularVelocity) * brakeDecay
```

В воздухе без drive/brake:

```text
angularVelocity *= 1 - dt
```

Конфиг:

- `friction` - `CombinedTireFrictionConfig`;
- `safetyGripMultiplier = 1.15`;
- `rollingResistance = 0.015`;
- `groundedAngularDamping = 0.02`;
- `airAngularDamping = 0.12`;
- `handbrakeTorque = 5000`.

## 19.1. `TireSlipCurve` и `CombinedTireFrictionConfig`

Кривая трения шины.

Поля:

- `extremumSlip`;
- `extremumValue`;
- `asymptoteSlip`;
- `asymptoteValue`.

Default longitudinal:

- `extremumSlip = 0.2`;
- `extremumValue = 1.0`;
- `asymptoteSlip = 0.8`;
- `asymptoteValue = 0.8`.

Default lateral:

- `extremumSlip = 8.0`;
- `extremumValue = 1.0`;
- `asymptoteSlip = 20.0`;
- `asymptoteValue = 0.8`.

`Evaluate(slip)`:

- берет `abs(slip)`;
- от 0 до extremum линейно растет от 0 до extremum value;
- от extremum до asymptote линейно падает/переходит к asymptote value;
- после asymptote возвращает asymptote value.

## 20. Unity-интеграция

### `VehicleController`

Компонент должен висеть на объекте с `Rigidbody`.

Поля инспектора:

- `vehicleConfigAsset` - `VehiclePreset`;
- `axleTransforms` - список осей сцены.

Для каждой оси в сцене задаются:

- `name`;
- `leftHardPoint`;
- `leftVisual`;
- `rightHardPoint`;
- `rightVisual`.

Hard point - локальная точка крепления подвески.
Visual - объект визуального колеса.

### Визуализация колес

`SyncWheelVisual()`:

1. Берет `hardPoint.localPosition`.
2. Смещает visual вниз по локальной Y на `suspension.currentLength`.
3. Добавляет визуальное вращение:

```text
rotationValue += tire.angularVelocity * Rad2Deg * Time.deltaTime
```

4. Собирает:

```text
spinRot = Quaternion.Euler(rotationValue, 0, 0)
steerRot = Quaternion.Euler(0, steeringAngle, 0)
visual.localRotation = steerRot * spinRot
```

Важно: визуализация не влияет на физику.

### `UnityVehicleHost`

Реализует `IVehicleHost`.

Читает:

- position/rotation из `Rigidbody.transform`;
- velocity/angular velocity из `Rigidbody`;
- point velocity через `Rigidbody.GetPointVelocity`.

Применяет:

```csharp
_rigidbody.AddForceAtPosition(force, position);
```

Raycast:

- вызывает `Physics.Raycast`;
- возвращает point, normal, distance;
- вычисляет velocity поверхности через attached rigidbody collider-а.

Sweep:

- создает или переиспользует `WheelSweeper`;
- `WheelSweeper` создает скрытый `GameObject` с kinematic `Rigidbody` и convex `MeshCollider`;
- mesh колеса строится как цилиндр;
- `SweepTest` проверяет контакт.

## 21. Ввод

Ввод сделан через Unity Input System, класс `GameInput` сгенерирован.

Action map: `Driving`.

Actions:

- `Throttle / Brake` - axis;
- `Steering` - axis;
- `Handbrake` - button;
- `Gearbox Down` - button;
- `Gearbox Up` - button;
- `Clutch` - button.

### Клавиатура

Throttle / Brake:

- `W` - газ;
- `S` - тормоз;
- `Up Arrow` - газ;
- `Down Arrow` - тормоз.

Steering:

- `A` - влево;
- `D` - вправо;
- `Left Arrow` - влево;
- `Right Arrow` - вправо.

Other:

- `Space` - handbrake;
- `Left Ctrl` - gearbox down;
- `Left Shift` - gearbox up;
- `Q` - clutch.

### Gamepad

Throttle / Brake:

- right trigger - газ;
- left trigger - тормоз.

Steering:

- left stick left/right.

Other:

- button south - handbrake;
- button east - gearbox down;
- button north - gearbox up;
- left shoulder - clutch.

### Особенность одноразовых shift-флагов

`VehicleController` хранит `_isUpshift` и `_isDownshift`.
В `Update()` они передаются в `UnityVehicleHost.SetInput()`, затем сбрасываются.

`UnityVehicleHost` кэширует эти флаги через OR:

```text
_cachedUpshift |= isUpshift
_cachedDownshift |= isDownshift
```

При `GetInput()` кэш сбрасывается.
Это защищает переключение от потери между `Update()` и `FixedUpdate()`.

## 22. Preset и конфиги модулей

### `VehiclePreset`

Это `ScriptableObject`:

```csharp
[CreateAssetMenu(fileName = "VehiclePreset", menuName = "Vehicle/Preset")]
```

Поля:

- `bodyMass = 1200`;
- `physicsSubSteps = 4`;
- `modules`;
- `axles`.

### Module entry

Каждая запись модуля содержит:

- `name` - генерируется в `Validate()`;
- `phase` - фаза модуля;
- `moduleLogic` - `SerializeReference` на `IVehicleModule`;
- `configAsset` - `ScriptableObject` с конфигом.

`SubclassSelector` используется для выбора реализации `IVehicleModule` в инспекторе.

### Создание core config

`CreateConfig()`:

1. Создает `VehicleConfig`.
2. Конвертирует оси через `AxleSetup.ToCore()`.
3. Для каждого модуля:
   - сериализует `moduleLogic` в JSON;
   - десериализует новый экземпляр того же типа;
   - если `configAsset` реализует `IModuleConfigAsset`, берет из него `ModuleConfig`;
   - проверяет, что config asset предназначен для этого типа модуля;
   - вызывает `moduleInstance.SetConfiguration(config)`;
   - кладет модуль в `config.modules`.

Так Unity asset остается шаблоном, а в runtime используется отдельный экземпляр модуля.

### `GeneratedConfigs`

Каждый generated config - это `ScriptableObject`, который реализует `IModuleConfigAsset`.

Паттерн:

```csharp
public Core.Modules.SomeModule.Config data = new Core.Modules.SomeModule.Config();
public ModuleConfig GetConfig() => data;
public Type GetTargetModuleType() => typeof(Core.Modules.SomeModule);
```

Созданные обертки есть для:

- `CylindricalRaycastModule`;
- `AntiRollBarModule`;
- `DrivingAssistsModule`;
- `EngineModule`;
- `GearboxModule`;
- `DrivetrainModule`;
- `MeshSweepModule`;
- `RaycastModule`;
- `SoftWheelContactModule`;
- `SteeringModule`;
- `SuspensionModule`;
- `TireModule`;
- `WideRaycastModule`.

## 23. Debug и тестовые инструменты

### `VehicleDebugger`

Компонент требует `VehicleController`.

Настройки:

- `toggleKey = F3`;
- `showOnStart = true`;
- `refreshRate = 0.05`.

Показывает:

- скорость в km/h;
- throttle/brake/steering;
- статус двигателя: running/stalled/cutoff;
- rpm bar;
- torque и load torque;
- текущую передачу и ratio;
- clutch engagement;
- массу;
- линейную и угловую скорость;
- по каждому колесу:
  - grounded/air;
  - compression;
  - suspension force;
  - slip ratio и slip angle;
  - long/lat force;
  - drive/brake torque.

### `RotatingPlatform`

Тестовый компонент для движущейся поверхности.

Особенности:

- требует `Rigidbody`;
- делает rigidbody kinematic;
- отключает gravity;
- вращает объект через `MoveRotation` в `FixedUpdate`.

Это позволяет Unity корректно рассчитывать velocity поверхности, а автомобиль получает ее через `hit.surfaceVelocity`.

## 24. Математика и Unity-адаптеры

### `Vec3`

Собственный вектор:

- операторы `+`, `-`, unary `-`, `*`, `/`;
- `Magnitude`;
- `SqrMagnitude`;
- `Normalize`;
- `Dot`;
- `Cross`;
- `ProjectOnPlane`;
- константы `Zero`, `One`, `Up`, `Right`, `Forward`.

### `Quat`

Собственный quaternion:

- поля `x`, `y`, `z`, `w`;
- `Identity`;
- `Euler`;
- умножение quaternion на quaternion;
- умножение quaternion на `Vec3`.

### Unity adapters

Под Unity доступны implicit conversion:

- `Vec3` <-> `UnityEngine.Vector3`;
- `Quat` <-> `UnityEngine.Quaternion`.

Это позволяет ядру работать со своими типами, а Unity-слою почти без ручного преобразования передавать данные в API Unity.

## 25. Типичный поток данных за физический кадр

Полный поток:

1. `VehicleController.Update()` собирает ввод.
2. `UnityVehicleHost.SetInput()` кэширует ввод.
3. `VehicleController.FixedUpdate()` вызывает `VehicleCore.Simulate()`.
4. `VehicleCore` читает ввод из хоста.
5. `VehicleCore` обновляет снимок `Rigidbody`.
6. `DrivingAssistsModule` меняет throttle/brake по прошлому slip.
7. `SoftWheelContactModule` или другой ground detection модуль обновляет контакт колес.
8. `SuspensionModule` считает нагрузку колес.
9. `AntiRollBarModule` корректирует левую/правую нагрузку оси.
10. `SteeringModule` считает рулевые углы.
11. `EngineModule` обновляет обороты и момент двигателя.
12. `GearboxModule` считает передачу, сцепление и выходной момент коробки.
13. `DrivetrainModule` распределяет момент по ведущим колесам.
14. `TireModule` в substeps считает combined slip и силы шин.
15. `VehicleCore` в каждом tire substep применяет tire force и normal/contact force к `Rigidbody`.
16. Следующий `Update()` синхронизирует visual wheels.

## 26. Практические заметки по настройке

### Если машина проваливается или слишком мягкая

Смотреть:

- `SuspensionModule.Config.restLength`;
- `springStiffness`;
- `bumpDamper`;
- `reboundDamper`;
- массу `VehiclePreset.bodyMass`;
- радиус колеса и hard point позиции.

Увеличение `springStiffness` держит кузов выше.
Увеличение `bumpDamper` уменьшает резкое сжатие.
Увеличение `reboundDamper` замедляет распрямление подвески.

### Если машина дергается на контакте

Смотреть:

- `physicsSubSteps`;
- тип модуля ground detection;
- `velocitySmoothing`;
- `safetyGripMultiplier`;
- `CombinedTireFrictionConfig`;
- кривые шин;
- корректность `wheel.radius`.

Для более стабильных шин обычно полезно увеличить `physicsSubSteps`.

### Если машина не едет

Проверить:

- есть ли powered axle;
- установлен ли `GearboxModule`;
- установлен ли `DrivetrainModule`;
- установлен ли `EngineModule`;
- установлен ли `TireModule`;
- не находится ли коробка в neutral (`currentGear = 1`);
- работает ли auto shift или был ли manual upshift;
- `clutch.engagement`;
- `engine.isRunning`;
- `engine.loadTorque`;
- `wheel.state.driveTorque`;
- `wheel.state.hit.isGrounded`.

### Если машина буксует слишком сильно

Смотреть:

- `CombinedTireFrictionConfig`;
- `TireSlipCurve`;
- `safetyGripMultiplier`;
- `CombinedTireFrictionConfig`;
- `DrivingAssistsModule.enableTCS`;
- `tcsSlipThreshold`;
- `tcsThrottleSensitivity`;
- `tcsBrakeSlipThreshold`;
- `tcsMaxBrakeForcePercent`;
- `GearboxModule.clutchSpeed`;
- блокировки и распределение момента в `DrivetrainModule`.

### Если тормоза слабые или блокируются

Смотреть:

- `DrivingAssistsModule.maxBrakeForce`;
- `enableABS`;
- `absSlipThreshold`;
- `absSensitivity`;
- `CombinedTireFrictionConfig`;
- `TireSlipCurve`;
- массу машины.

### Если руление слишком резкое

Смотреть:

- `SteeringModule.maxSteeringAngle`;
- `steerSpeed`;
- `recenteringSpeed`;
- `ackermannFactor`;
- режимы `SteeringMode` у осей.

### Если контакт с землей плохой

Выбор ground module:

- `RaycastModule` - дешевый и простой.
- `WideRaycastModule` - лучше для ширины колеса.
- `CylindricalRaycastModule` - лучше имитирует нижнюю часть колеса.
- `MeshSweepModule` - использует объемную sweep-проверку через Unity physics.

Нужно следить, чтобы в пресете не было одновременно нескольких ground detection модулей, если это не задумано специально. Каждый такой модуль переписывает `wheel.state.hit`, и победит тот, который выполнится позже в отсортированном порядке модулей с одинаковой phase фактически зависит от порядка сортировки/списка.

## 27. Как добавлять новый модуль

Новый модуль должен:

1. Реализовать `IVehicleModule`.
2. Вернуть нужную `VehicleModulePhase`.
3. Иметь вложенный `[Serializable] class Config : ModuleConfig`, если нужны настройки.
4. Реализовать `SetConfiguration(ModuleConfig config)`.
5. В `Initialize(VehicleContext context)` подготовить кеши, массивы и начальные значения.
6. В `Update(VehicleContext context, float dt)` читать и писать только нужные части контекста.
7. Создать Unity `GeneratedConfig`-обертку, если модуль должен настраиваться через inspector.
8. Добавить модуль в `VehiclePreset.modules`.

Пример pattern:

```csharp
[Serializable]
public class MyModule : IVehicleModule
{
    private Config _config = new();
    public VehicleModulePhase Phase => VehicleModulePhase.SomePhase;

    public void SetConfiguration(ModuleConfig config)
    {
        if (config is Config typed) _config = typed;
    }

    public void Initialize(VehicleContext context)
    {
    }

    public void Update(VehicleContext context, float dt)
    {
    }

    [Serializable]
    public class Config : ModuleConfig
    {
    }
}
```

## 28. Важные текущие особенности и ограничения

- `SoftWheelContactModule` является рекомендуемым ground/contact модулем для simcade-настроек; старые raycast-модули можно оставлять для debug/legacy.
- `SuspensionModule.Config.damperStiffness` сейчас не используется.
- `TireModule` больше не содержит dummy drivetrain torque; приводной момент должен приходить из `DrivetrainModule`.
- `TireModule.Config.brakeTorque` сейчас не используется как основной источник тормоза.
- Фазы `AntiRollBar` и `Differential` объявлены, но соответствующих модулей в проекте нет.
- При нескольких модулях одной phase порядок зависит от сортировки списка по phase и исходного порядка элементов с одинаковым значением.
- `DrivingAssistsModule` использует slip предыдущего физического кадра, потому что стоит перед `Tire`.
- `MeshSweepModule` создает скрытые GameObject-ы sweeper-ов на каждый wheel index.
- `VehicleController` требует совпадения количества осей в пресете и в `axleTransforms`.
- Визуальные колеса следуют состоянию подвески и шин, но не участвуют в физике.

## 29. Минимальный набор для рабочей машины

В сцене:

- GameObject с `Rigidbody`;
- `VehicleController`;
- назначенный `VehiclePreset`;
- `axleTransforms` с hard points и visual wheels.

В `VehiclePreset`:

- масса кузова;
- минимум 2 оси;
- корректные wheel radius/width/mass;
- хотя бы одна powered axle;
- один ground detection модуль;
- `SuspensionModule`;
- `SteeringModule`;
- `EngineModule`;
- `GearboxModule`;
- `DrivetrainModule`;
- `TireModule`;
- опционально `DrivingAssistsModule`.

Обычно порядок модулей по phase будет таким:

1. `DrivingAssistsModule`
2. один из ground detection модулей
3. `SuspensionModule`
4. `SteeringModule`
5. `EngineModule`
6. `GearboxModule`
7. `DrivetrainModule`
8. `TireModule`

Фактически `VehicleCore` сам сортирует их по `VehicleModulePhase`.

## 30. Где смотреть при отладке

Быстрые точки наблюдения:

- `VehicleDebugger` в игре, клавиша F3.
- `ctx.engine.rpm`, `ctx.engine.isRunning`, `ctx.engine.loadTorque`.
- `ctx.transmission.currentGear`, `ctx.transmission.currentGearRatio`, `ctx.clutch.engagement`.
- `wheel.state.hit.isGrounded`.
- `wheel.state.suspension.force`.
- `wheel.state.tire.slipRatio`, `slipAngle`, `totalForce`.
- `wheel.state.driveTorque`, `brakeTorque`.

Если нужно понять, почему сила не прикладывается:

1. Проверить grounded.
2. Проверить suspension force.
3. Проверить load и tire force.
4. Проверить, что `ApplyForce` вызывается через `UnityVehicleHost`.
5. Проверить, что `Rigidbody` не kinematic и масса адекватна.
