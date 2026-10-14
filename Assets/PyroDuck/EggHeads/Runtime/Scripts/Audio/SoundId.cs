namespace com.pyroduck.eggheads.Runtime.Scripts.Audio
{
    /// <summary>
    /// Central identifier for every sound the audio system can play.
    /// Add new entries at the end to keep serialized values stable.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // Ranged weapons
        ProjectileFire      = 100,
        ProjectileImpact    = 101,
        ProjectileExplosion = 102,

        // Throwable weapons
        ThrowableThrow      = 200,
        ThrowableImpact     = 201,
        ThrowableExplosion  = 202,

        // Melee weapons
        MeleeSwing          = 300,
        MeleeImpact         = 301,

        // UI and shared weapon handling
        UiClick             = 400,
        WeaponEquip         = 401,
        WeaponDrop          = 402,
        WeaponHit           = 403,
        WeaponReload        = 404,

        // Specific ranged weapon clips
        PistolFire          = 500,
        RevolverFire        = 501,
        RifleFire           = 502,
        ShotgunFire         = 503,
        ShotgunRecoil       = 504,
        SniperFire          = 505,
        LaserFire           = 506,
        BubbleShot          = 507,
        RangedRecoil        = 508,
        RocketFire          = 509,

        // Specific throwable / motion clips
        BoomerangFlight     = 600,
        ShurikenFlight      = 601,
        RotatingFlight      = 602,

        // Generic grouped clips
        Explosion           = 700,
    }
}
