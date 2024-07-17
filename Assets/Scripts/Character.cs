using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using System;
using Helper;
using TMPro;

namespace game
{ 
    public enum ERotationBehavior
    {
        OrientRotationToMovement,
        UseControlRotation
    }
    public enum SurfaceState
    {
        OnGround,
        OnSurface,
        InAir
    }

    [System.Serializable]
    public record RotationSettings
    {
        [HideInInspector]
        public SubstateMachine substates;

        [Header("Booleans")]



        [Header("Control Rotation")]
        public float minPitchAngle = -45.0f;
        public float maxPitchAngle = 75.0f;

        [Header("Character Orientation")]
        public ERotationBehavior rotationBehavior = ERotationBehavior.OrientRotationToMovement;
        public float minRotationSpeed = 600.0f; // The turn speed when the player is at max speed (in degrees/second)
        public float maxRotationSpeed = 1200.0f; // The 

    }

    [System.Serializable]
    public record MovementSettings
    {
        [HideInInspector]
        public SubstateMachine substates;

        public float acceleration = 25.0f;
        public float decceleration = 25.0f;
        public float maxHorizontalSpeed = 8.0f;
        public float jumpSpeed = 10.0f;
        public float jumpAbortSpeed = 10.0f;
        public float coyoteTime = .005f;
        public float targetHorizontalSpeed;
        public float movementInputScaler = 1f;

    }

    [System.Serializable]
    public record GravitySettings
    {
        public float gravity = 20.0f;
        public float groundedGravity = 5.0f;
        public float maxFallSpeed = 40.0f;
    }

    // works really well with the substate system
    // this way the set of variables can be easily managed by the substatemachine
    // You can easily package and add state/ functions to modifiy these packaged variable sets 
    // this organizes variable manipulation and stays modular
    // flexible system to allow lots of behaviour 
    [System.Serializable]
    public record SurfaceCollisions
    {
        [HideInInspector]
        public SubstateMachine substates;
        public Vector2 characterColliderScale;
        public bool justWalkedOffEdge;
        public Vector3 currentNormal;
        public float castDistance = 1;
       
        public LayerMask surfaceLayers;
        public int numOfBoxes = 1; // arr max
        public Vector3[] boxCastPositions = new Vector3[] { Vector3.zero };
        public Vector3[] boxCastScale = new Vector3[] { Vector3.zero }; 
        public Vector3[] boxRotations = new Vector3[] { Vector3.zero }; 
        public Vector3[] surfaceNormals = new Vector3[] { Vector3.zero };

    } 
    // ^can likely do: similiar set up for hit box set up^

    public class Character : MonoBehaviour
    {
        public Controller controller;
        public MovementSettings movement;
        public GravitySettings gravitySettings;
        public SurfaceCollisions surfaceCollisions;
        public RotationSettings rotationSettings;
 
        private CharacterController characterController;
        //private CharacterAnimator characterAnimator;

       private float horizontalSpeed;
        private float verticalSpeed;

        private Vector2 controlRotation; //X(pitch), Y(yaw)
        private Vector3 movementInput;
        private Vector3 lastMovementInput;
        private bool hasMovementIn;
        private bool jumpIn;
       
        private RaycastHit hit;
        private (int, int) currentSurface;
        private (int, int) lastSurface;

        public Vector3 velocity => characterController.velocity;
        public Vector3 horizontalVelocity => characterController.velocity.SetY(0.0f);
        public Vector3 verticalVelocity => characterController.velocity.Multiply(0.0f, 1.0f, 0.0f);
        public Vector2 characterColliderScale => new Vector2(characterController.radius, characterController.height); 
        public Vector3 checkPosition => transform.position + new Vector3(0, -1, 0);

        private Func<Vector3> _applyMovement;
        public Func<Vector3> applyMovement {
            get
            {
                if(_applyMovement == null)
                    return () => GetMovementInput() * horizontalSpeed + Vector3.up * verticalSpeed;
                Func<Vector3> hold = _applyMovement;
                _applyMovement = null;
                return hold; 
            }
            set { _applyMovement = value; }
        }
        public Func<float, float>? SetVerticalSpeed;
        public Func<float, float>? SetHorizontalSpeed;
        public float getVerticalSpeed => verticalSpeed;
        public float getHorizontalSpeed => horizontalSpeed;
        public bool isJumpInput => jumpIn;
        public bool hasMovementInput => hasMovementIn;
        // should turn this into a static var

        private void Awake()
        {
            characterController = gameObject.GetComponent<CharacterController>();
            controller.Init(this); 
            //characterAnimator = gameObject.GetComponent<CharacterAnimator>();

        }
 
        private void Update()
        {
            controller.OnCharacterUpdate();
        }

        private void FixedUpdate()
        {
            controller.OnCharacterFixedUpdate();

            Tick(Time.deltaTime);
            
           
        }

        private void OnDrawGizmos()
        {
            for(int i = 0; i < surfaceCollisions.numOfBoxes; i++)
            {
                Gizmos.DrawRay(transform.position, surfaceCollisions.boxRotations[i].normalized * 1f);

                DrawCollisionBox(i);


            }
        }

        private void DrawCollisionBox(int index)
        {
            // Draw the box using Gizmos
            Vector3 center = transform.position + surfaceCollisions.boxCastPositions[index];
            Vector3 halfExtents = surfaceCollisions.boxCastScale[index] * 0.5f;
            Vector3 rotation = surfaceCollisions.boxRotations[index];

            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(rotation), Vector3.one);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity; // Reset matrix to avoid affecting other Gizmos calls

        }
        // All functions to local functions to run on ficked update, used to pass delta time to all these functions 
        private void Tick(float deltaTime)
        {
            CheckSurfaces(); 
            surfaceCollisions.substates.RunCurrent();
            this.movement.substates.RunCurrent();

            UpdateVerticalSpeed(deltaTime);
            UpdateHorizontalSpeed(deltaTime);

            Vector3 movement = applyMovement();
            characterController.Move(movement * deltaTime);

            OrientToTargetRotation(movement.SetY(0.0f), deltaTime);
//            _characterAnimator.UpdateState();
        }

        public void SetMovementInput(Vector3 movementInput)
        {
            bool hasMovementIn = movementInput.sqrMagnitude > 0.0f; 
            if (!this.hasMovementIn && hasMovementInput ) 
            {
                lastMovementInput = this.movementInput;
            }

            this.movementInput = movementInput;
            this.hasMovementIn = hasMovementIn;
        }

        
        public Vector3 GetMovementInput()
        {
            Vector3 movementInput = hasMovementIn ? this.movementInput : lastMovementInput;
            if(movementInput.sqrMagnitude > 1f)
            {
                movementInput.Normalize();
            }
            return movementInput; 
        }
        
        public void SetJumpInput(bool jumpIn)
        {
            this.jumpIn = jumpIn;
        }

        public Vector2 GetControlRotation()
        {
            return controlRotation;
        }

        public void SetControlRotation(Vector2 controlRotation)
        {
            float pitchAngle = controlRotation.x;
            pitchAngle %= 360.0f;
            pitchAngle = Mathf.Clamp(pitchAngle, rotationSettings.minPitchAngle, rotationSettings.maxPitchAngle);

            float yawAngle = controlRotation.y;
            yawAngle %= 360.0f;

            this.controlRotation = new Vector2(pitchAngle, yawAngle); 
        }

        private bool CheckSurfaces() 
        { 
            Vector3 direction;
            Vector3 position;
            Quaternion localRotation;
            bool gotHit = false;
            for (int i = 0; i < surfaceCollisions.numOfBoxes; i++)
            { 
                // quaternion point rotation
                position = surfaceCollisions.boxCastPositions[i];
                localRotation = new Quaternion(position.x, position.y, position.z, 0);
                localRotation = transform.rotation * localRotation * Quaternion.Inverse(transform.rotation); 
                direction = new Vector3(localRotation.x, localRotation.y, localRotation.z);
                surfaceCollisions.boxRotations[i] = direction;

                // box cast is not working properly 
                gotHit = true == Physics.BoxCast(transform.position, surfaceCollisions.boxCastScale[i], direction, out hit, transform.rotation, surfaceCollisions.castDistance, surfaceCollisions.surfaceLayers); 
                surfaceCollisions.surfaceNormals[i] = hit.normal;

                // update surface info if moving into a surface
                if (gotHit && i > 0 && Vector3.Distance(GetMovementInput(), hit.normal) > 1)
                { 
                    if (currentSurface.Item2 != lastSurface.Item2)
                        lastSurface = currentSurface;
                    else
                    {
                        currentSurface.Item1 = i;
                        currentSurface.Item2 = hit.collider.GetHashCode();
                    }
                    surfaceCollisions.currentNormal = hit.normal;
                }
                else
                    surfaceCollisions.currentNormal = Vector3.zero;
            }

            return gotHit;
        }



// so the first time touching it works and starts reading the inputs 

        // try and set it up so if there is no given update func then it runs a default vert and horizontal update:
        private void UpdateVerticalSpeed(float deltaTime)
        {
            if (SetVerticalSpeed == null)
                verticalSpeed = getVerticalSpeed;
            else
            { 
                verticalSpeed = SetVerticalSpeed(deltaTime);
            }
        }
 
        private void UpdateHorizontalSpeed(float deltaTime)
        {
            if(SetHorizontalSpeed == null)
                horizontalSpeed = getHorizontalSpeed;
            else
                horizontalSpeed = SetHorizontalSpeed(deltaTime);
        }

        private void OrientToTargetRotation(Vector3 horizontalMovement, float deltaTime) 
        {

            // might have to add the SurfaceState switch to change a variable for this function 

            if (horizontalMovement != Vector3.zero)
            {
                if (rotationSettings.rotationBehavior == ERotationBehavior.OrientRotationToMovement && horizontalMovement.sqrMagnitude > 0.0f)
                {
                    float rotationSpeed = Mathf.Lerp(
                        rotationSettings.maxRotationSpeed, rotationSettings.minRotationSpeed, horizontalSpeed / movement.targetHorizontalSpeed);

                    Quaternion targetRotation = Quaternion.LookRotation(horizontalMovement, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * deltaTime);
                }
                else if (rotationSettings.rotationBehavior == ERotationBehavior.UseControlRotation)
                {
                    Quaternion targetRotation = Quaternion.Euler(0.0f, controlRotation.y, 0.0f);
                    transform.rotation = targetRotation;
                }
            }
        }






    }
}

