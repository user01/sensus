﻿// Copyright 2014 The Rector & Visitors of the University of Virginia
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using Xamarin.Forms;
using System.Collections.Generic;
using SensusService.Exceptions;
using System.Linq;
using SensusUI.Inputs;
using System.Threading;

namespace SensusUI
{
    public class PromptForInputsPage : ContentPage
    {
        public enum Result
        {
            NavigateBackward,
            NavigateForward,
            Cancel
        }

        private int _displayedInputCount;

        public int DisplayedInputCount
        {
            get
            {
                return _displayedInputCount;
            }
        }

        public PromptForInputsPage(InputGroup inputGroup, int stepNumber, int totalSteps, bool showCancelButton, string nextButtonTextOverride, CancellationToken? cancellationToken, string cancelConfirmation, string incompleteSubmissionConfirmation, string submitConfirmation, Action<Result> callback)
        {            
            _displayedInputCount = 0;

            float progress = (stepNumber - 1) / (float)totalSteps;

            StackLayout contentLayout = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                VerticalOptions = LayoutOptions.FillAndExpand,
                Padding = new Thickness(0, 20, 0, 0),
                Children =
                {
                    new Label
                    {
                        Text = inputGroup.Name,
                        FontSize = 20,
                        HorizontalOptions = LayoutOptions.CenterAndExpand
                    },
                    new Label
                    {
                        Text = "Progress:  " + Math.Round(100 * progress) + "%",
                        FontSize = 15,
                        HorizontalOptions = LayoutOptions.CenterAndExpand
                    },
                    new ProgressBar
                    {
                        Progress = progress,
                        HorizontalOptions = LayoutOptions.FillAndExpand
                    }
                }
            };

            int viewNumber = 1;
            bool anyRequired = false;
            List<Input> displayedInputs = new List<Input>();
            foreach (Input input in inputGroup.Inputs)
                if (input.Display)
                {
                    View inputView = input.GetView(viewNumber);
                    if (inputView != null)
                    {
                        contentLayout.Children.Add(inputView);
                        displayedInputs.Add(input);

                        if (input.DisplayNumber)
                            ++viewNumber;

                        if (input.Required)
                            anyRequired = true;

                        ++_displayedInputCount;
                    }
                }

            if (anyRequired)
                contentLayout.Children.Add(new Label
                    {
                        Text = "* Required Field",
                        FontSize = 15,
                        TextColor = Color.Red,
                        HorizontalOptions = LayoutOptions.FillAndExpand
                    });

            StackLayout navigationStack = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            #region previous button

            bool previousButtonTapped = false;

            // step numbers are 1-based -- if we're beyond the first, provide a previous button
            if (stepNumber > 1)
            {
                Button previousButton = new Button
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    FontSize = 20,
                    Text = "Previous"
                };

                navigationStack.Children.Add(previousButton);

                previousButton.Clicked += async (o, e) =>
                {
                    previousButtonTapped = true;
                    await Navigation.PopModalAsync(false);
                };                      
            }

            #endregion

            #region cancel button

            bool cancelButtonTapped = false;

            if (showCancelButton)
            {
                Button cancelButton = new Button
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    FontSize = 20,
                    Text = "Cancel"
                };

                navigationStack.Children.Add(cancelButton);

                cancelButton.Clicked += async (o, e) =>
                {
                    string confirmationMessage = "";

                    if (!string.IsNullOrWhiteSpace(cancelConfirmation))
                        confirmationMessage += cancelConfirmation;

                    if (string.IsNullOrWhiteSpace(confirmationMessage) || await DisplayAlert("Confirm", confirmationMessage, "Yes", "No"))
                    {
                        // if the cancellation token was cancelled while the dialog was up, then we should ignore the dialog. the token
                        // will have already popped this page off the navigation stack.
                        if (!cancellationToken.GetValueOrDefault().IsCancellationRequested)
                        {
                            cancelButtonTapped = true;
                            await Navigation.PopModalAsync(true);
                        }
                    }
                };
            }

            #endregion

            #region next button

            Button nextButton = new Button
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,
                FontSize = 20,
                Text = stepNumber < totalSteps ? "Next" : "Submit"

                #if UNIT_TESTING
                // set style id so that we can retrieve the button when unit testing
                , StyleId = "NextButton"
                #endif
            };

            if (nextButtonTextOverride != null)
                nextButton.Text = nextButtonTextOverride;

            navigationStack.Children.Add(nextButton);

            bool nextButtonTapped = false;

            nextButton.Clicked += async (o, e) =>
            {
                string confirmationMessage = "";

                if (!string.IsNullOrWhiteSpace(incompleteSubmissionConfirmation) && !inputGroup.Valid)
                    confirmationMessage += incompleteSubmissionConfirmation;
                else if (nextButton.Text == "Submit" && !string.IsNullOrWhiteSpace(submitConfirmation))
                    confirmationMessage += submitConfirmation;
                    
                if (string.IsNullOrWhiteSpace(confirmationMessage) || await DisplayAlert("Confirm", confirmationMessage, "Yes", "No"))
                {
                    // if the cancellation token was cancelled while the dialog was up, then we should ignore the dialog. the token
                    // will have already popped this page off the navigation stack.
                    if (!cancellationToken.GetValueOrDefault().IsCancellationRequested)
                    {
                        nextButtonTapped = true;
                        await Navigation.PopModalAsync(stepNumber == totalSteps);
                    }
                }
            };

            #endregion
                
            contentLayout.Children.Add(navigationStack);

            #region cancellation token

            bool cancellationTokenCanceled = false;

            if (cancellationToken != null)
            {
                // if the cancellation token is cancelled, pop this page off the stack.
                cancellationToken.GetValueOrDefault().Register(() =>
                    {                        
                        cancellationTokenCanceled = true;

                        Device.BeginInvokeOnMainThread(async() =>
                            {
                                if (Navigation.ModalStack.Count > 0 && Navigation.ModalStack.Last() == this)
                                    await Navigation.PopModalAsync(true);
                            });
                    });
            }

            #endregion

            Appearing += (o, e) =>
            {
                foreach (Input input in displayedInputs)
                    input.Viewed = true;
            };
            
            Disappearing += (o, e) =>
            {
                if (previousButtonTapped)
                    callback(Result.NavigateBackward);
                else if (cancelButtonTapped || cancellationTokenCanceled)
                    callback(Result.Cancel);
                else if (nextButtonTapped)
                    callback(Result.NavigateForward);
                else
                    callback(Result.Cancel);  // the user navigated back, or another activity started
            };                    

            Content = new ScrollView
            {
                Content = contentLayout
            };                        
        }
    }
}