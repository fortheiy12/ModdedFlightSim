﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plane : MonoBehaviour {
    [SerializeField]
    float maxThrust;
    [SerializeField]
    float throttleSpeed;

    [SerializeField]
    float liftPower;
    [SerializeField]
    AnimationCurve liftAOACurve;
    [SerializeField]
    AnimationCurve inducedDragCurve;

    [SerializeField]
    AnimationCurve dragForward;
    [SerializeField]
    AnimationCurve dragBack;
    [SerializeField]
    AnimationCurve dragLeft;
    [SerializeField]
    AnimationCurve dragRight;
    [SerializeField]
    AnimationCurve dragTop;
    [SerializeField]
    AnimationCurve dragBottom;

    float throttleInput;

    public Rigidbody Rigidbody { get; private set; }
    public float Throttle { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector3 LocalVelocity { get; private set; }
    public float AngleOfAttack { get; private set; }
    public float AngleOfAttackYaw { get; private set; }

    void Start() {
        Rigidbody = GetComponent<Rigidbody>();
    }

    public void SetThrottleInput(float input) {
        throttleInput = input;
    }

    void UpdateThrottle(float dt) {
        float target = 0;
        if (throttleInput > 0) target = 1;

        //throttle input is [-1, 1]
        //throttle is [0, 1]
        Throttle = Utilities.MoveTo(Throttle, target, throttleSpeed * Mathf.Abs(throttleInput), dt);
    }

    void CalculateAngleOfAttack() {
        AngleOfAttack = Mathf.Atan2(-LocalVelocity.y, LocalVelocity.z);
        AngleOfAttackYaw = Mathf.Atan2(LocalVelocity.x, LocalVelocity.z);
    }

    void CalculateState() {
        Velocity = Rigidbody.velocity;
        LocalVelocity = Quaternion.Inverse(Rigidbody.rotation) * Velocity;  //transform world velocity into local space

        CalculateAngleOfAttack();
    }

    void UpdateThrust() {
        Rigidbody.AddRelativeForce(Throttle * maxThrust * Vector3.forward);
    }

    void UpdateDrag() {
        var lv = LocalVelocity;
        var lv2 = lv.sqrMagnitude;  //velocity squared

        //calculate coefficient of drag depending on direction on velocity
        var coefficient = Utilities.Scale6(
            lv.normalized,
            dragRight.Evaluate(lv.x), dragLeft.Evaluate(-lv.x),
            dragTop.Evaluate(lv.y), dragBottom.Evaluate(-lv.y),
            dragForward.Evaluate(lv.z), dragBack.Evaluate(-lv.z)
        );

        var drag = coefficient.magnitude * lv2 * -lv.normalized;    //drag is opposite direction of velocity

        Rigidbody.AddRelativeForce(drag);
    }

    Vector3 CalculateLift(float angleOfAttack, Vector3 rightAxis, float liftPower, AnimationCurve aoaCurve, AnimationCurve inducedDragCurve) {
        //lift = velocity^2 * coefficient * liftPower
        //coefficient varies with AOA
        var liftVelocity = Vector3.ProjectOnPlane(LocalVelocity, rightAxis);   //project velocity onto YZ plane
        var liftCoefficient = aoaCurve.Evaluate(angleOfAttack * Mathf.Rad2Deg);
        var liftForce = liftVelocity.sqrMagnitude * liftCoefficient * liftPower;

        //lift is perpendicular to velocity
        var liftDirection = Vector3.Cross(Rigidbody.velocity.normalized, rightAxis);
        var lift = liftDirection * liftForce;

        //induced drag varies with square of lift coefficient
        var dragForce = liftCoefficient * liftCoefficient;
        var dragDirection = -liftVelocity.normalized;
        var inducedDrag = dragDirection * dragForce * inducedDragCurve.Evaluate(Mathf.Max(0, LocalVelocity.z));

        return lift + inducedDrag;
    }

    void UpdateLift() {
        if (LocalVelocity.sqrMagnitude < 1f) return;

        var liftForce = CalculateLift(
            AngleOfAttack, Vector3.right,
            liftPower,
            liftAOACurve,
            inducedDragCurve
        );

        Rigidbody.AddRelativeForce(liftForce);
    }

    void FixedUpdate() {
        float dt = Time.fixedDeltaTime;

        CalculateState();

        //handle user input
        UpdateThrottle(dt);

        //apply updates
        UpdateThrust();
        UpdateLift();

        UpdateDrag();

        CalculateState();
    }
}