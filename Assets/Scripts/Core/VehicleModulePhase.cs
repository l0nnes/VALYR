namespace Core
{
    public enum VehicleModulePhase : byte
    {
        InputModifiers = 0, // ABS, ESP, Drift Assist
        GroundDetection = 10, // Wheel-ground contact
        Suspension = 20, // Пружины и амортизаторы
        AntiRollBar = 25, // Стабилизаторы поперечной устойчивости
        Steering = 30, // Рулевая рейка (Аккерман)
        Engine = 40, // ДВС, Турбо, Нитро
        Transmission = 50, // Сцепление, КПП
        Differential = 60, // Разделение момента по осям
        Tire = 70 // Расчет сил трения колес (Sub-stepped)
    }
}