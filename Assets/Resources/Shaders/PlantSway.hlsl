// The sway and the player's push for Swaying Plant, done on the GPU so a field of seaweed costs the CPU nothing.
// Included by every pass, so the colour, the depth and the shadow all move together.
#ifndef OOTD_PLANT_SWAY_INCLUDED
#define OOTD_PLANT_SWAY_INCLUDED

// Per plant (its material).
float4 _SwayDirection;   // xz: which way the current pushes it
float _SwayAmount;       // metres the top moves in the ordinary current
float _SwaySpeed;        // cycles per second
float _TipLag;           // how far the tip trails the base, as a fraction of a cycle
float _PlantHeight;      // metres from the base (object y 0) to the top: bending grows up to here
float _Gust;             // an extra push along the current right now (a swoop), in units of _SwayAmount
float _PushReach;        // metres beyond the player's body a leaf is pushed aside

// The player (Swim Controller sets these every frame): xyz = the middle of their body, w = its radius; and half
// its height.
float4 _PlantPusher;
float _PlantPusherHalf;

float3 PlantSway(float3 positionOS)
{
    float h = saturate(positionOS.y / max(_PlantHeight, 0.01));
    float3 positionWS = TransformObjectToWorld(positionOS);

    // The current: rooted at the base, moving most at the top, the top trailing, and a slow wave that travels
    // through the clump (so neighbours are a little out of step).
    float bend = pow(h, 1.7);
    float travel = dot(positionWS.xz, float2(0.11, 0.07));
    float wave = sin((_Time.y * _SwaySpeed - h * _TipLag + travel) * 6.28318);
    float3 along = float3(_SwayDirection.x, 0, _SwayDirection.z);
    along = along / max(length(along), 0.0001);
    float3 offset = along * (_SwayAmount * (wave + _Gust) * bend);

    // The player's body: leaves near it are pushed straight out of the way, strongest at the height the body is at
    // and fading a little above and below it; the base stays rooted.
    float2 away = positionWS.xz - _PlantPusher.xz;
    float distance = length(away);
    float reach = _PlantPusher.w + _PushReach;
    float band = saturate(1.0 - abs(positionWS.y - _PlantPusher.y) / (_PlantPusherHalf + 0.8));
    float amount = max(0.0, reach - distance) * band * saturate(h * 5.0);
    offset.xz += (distance > 0.0001 ? away / distance : float2(1, 0)) * amount;

    // A bent stem does not get longer.
    offset.y -= dot(offset.xz, offset.xz) * 0.5 / max(_PlantHeight, 0.5);
    return TransformWorldToObject(positionWS + offset);
}

#endif
