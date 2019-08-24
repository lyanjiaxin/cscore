﻿using com.csutil;
using com.csutil.testing;
using com.csutil.tests;
using Mopsicus.InfiniteScroll;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class XunitTestRunnerUi : MonoBehaviour {

    public int defaultEntryHeight = 80;
    public int timeoutInMs = 30000;
    private List<XunitTestRunner.Test> allTests;

    private void OnEnable() {
        var links = gameObject.GetLinkMap();
        var listUi = links.Get<InfiniteScroll>("HorizontalScrollView");
        listUi.OnHeight += OnHeight;
        listUi.OnFill += OnFill;
        links.Get<Button>("StartButton").SetOnClickAction(delegate { StartCoroutine(RunAllTests(listUi)); });
    }

    private int OnHeight(int index) {
        return defaultEntryHeight;
    }

    private void OnFill(int pos, GameObject view) {
        var test = allTests[pos];
        var links = view.GetLinkMap();
        links.Get<Text>("Name").text = test.methodToTest.ToStringV2();
        if (test.testTask == null) {
            links.Get<Text>("Status").text = "Not started yet";
            links.Get<Image>("StatusColor").color = Color.white;
        } else if (test.testTask.IsFaulted) {
            links.Get<Text>("Status").text = "Error: " + test.reportedError;
            links.Get<Image>("StatusColor").color = Color.red;
            Log.e("" + test.reportedError);
        } else if (test.testTask.IsCompleted) {
            links.Get<Text>("Status").text = "Passed";
            links.Get<Image>("StatusColor").color = Color.green;
        } else {
            links.Get<Text>("Status").text = "Running..";
            links.Get<Image>("StatusColor").color = Color.blue;
        }
    }

    public IEnumerator RunAllTests(InfiniteScroll listUi) {
        var errorCollector = new LogForXunitTestRunnerInUnity();
        var allClasses = typeof(MathTests).Assembly.GetExportedTypes();
        allTests = XunitTestRunner.CollectAllTests(allClasses, delegate {
            //// setup before each test, use same error collector for all tests:
            Log.instance = errorCollector;
        });
        AssertV2.AreNotEqual(0, allTests.Count);
        listUi.InitData(allTests.Count);
        foreach (var startedTest in allTests) {
            startedTest.StartTest();
            yield return new WaitForEndOfFrame();
            listUi.UpdateVisible();
            yield return startedTest.testTask.AsCoroutine((e) => { Debug.LogError(e); }, timeoutInMs);
            yield return new WaitForEndOfFrame();
            listUi.UpdateVisible();
        }
        AssertV2.AreEqual(0, allTests.Filter(t => t.testTask.IsFaulted).Count());
    }

}