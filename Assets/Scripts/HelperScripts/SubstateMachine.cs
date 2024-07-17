using System;
using System.Collections.Generic;
using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;
namespace Helper
{
    public class SubstateMachine
    {
        public delegate void MyStates();

        private string _currentState;
        private List<MyStates> stateActions;
        private Dictionary<string, int> stateMap;
        public bool lockState = false;

        // Next step to evolve this is to make it so that ther are closing functions that are easily added to a state

        // would be super cool to just give a little pocket of memory  to pass around

        public int currentStateIndex => stateMap[currentState]; // this is giving void sometimes 
        public string currentState
        {
            get
            {
                if (_currentState == null)
                    return "Void";
                return _currentState; }
            set
            {

                if (!lockState && _currentState != value)
                {

                    if (stateMap.ContainsKey(value))
                    { _currentState = value; }

                    else
                        _currentState = "Void";
                }
            }
        }

        /// <summary>
        /// Event based state machine, portable and modular
        /// intended to be used in conjunction with controller records 
        /// </summary>
        public SubstateMachine()
        {
            stateActions = new List<MyStates>();
            stateMap = new Dictionary<string, int>();
        }

        public void AddState(MyStates state)
        {
            stateActions.Add(state);
            stateMap.Add(state.Method.Name, stateActions.Count - 1);
        }

        public MyStates GetState(string name)
        {
            if (stateMap.ContainsKey(name))
            {
                return stateActions[stateMap[name]];
            }
            else
            {
                Debug.Log("ERROR: State " + name + " not in substate machine");
                return null;
            }
        }

        public void SetCurrent(MyStates state)
        {
            string name = state.Method.Name;
            if (stateMap.ContainsKey(name))
                currentState = name;
        }
        public void RunCurrent()
        {
            int index = currentStateIndex;
            if (index >= 0 && index < stateActions.Count - 1)
            {
                stateActions[index].Invoke();
            } 
        }
 

        public void Run(string name)
        {
            MyStates state = GetState(name);
            if (state != null)
            {

                currentState = name;
                state.Invoke();
            }
        }

        public void Run(MyStates state)
        {
            string name = state.Method.Name;
            if (state != null && stateMap.ContainsKey(name))
            {
                currentState = name;
                state.Invoke();
            }
        }


        public IEnumerator HoldCurrentStateTill(float time)
        {
            lockState = true;
            yield return new WaitForSeconds(time);
            lockState = false;
        }

        public IEnumerator HoldCurrentStateTill(Func<bool> action)
        {
            lockState = true;
            yield return new WaitUntil(() => action());
            lockState = false;
        }

        public IEnumerator HoldCurrentStateTill()
        {
            lockState = true;
            yield return new WaitForFixedUpdate();
            lockState = false;
        }

        // need some logic to catch switch states belonging to a different sbm
        public IEnumerator SwitchToAfter(Func<bool> switchCase, MyStates switchState)
        {
            lockState = true;
            yield return new WaitUntil(() => switchCase());
            lockState = false;
            Run(switchState);
        }

        public IEnumerator SwitchToAfter(float time, MyStates switchState)
        {
            lockState = true;
            yield return new WaitForSeconds(time);
            lockState = false;
            Run(switchState);
        }

        public static bool operator==(SubstateMachine stateMachine, MyStates state)
        {
            return stateMachine.currentState == state.Method.Name;
        }
        public static bool operator!=(SubstateMachine stateMachine, MyStates state)
        {
            return stateMachine.currentState != state.Method.Name;
        }
    }
}
