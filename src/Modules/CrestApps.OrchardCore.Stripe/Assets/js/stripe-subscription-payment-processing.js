stripePaymentProcessing = function () {

    const initialize = (options) => {

        const defaultOptions = {
            processorKey: 'Stripe',
            cardElement: '#card-element'
        };

        const config = { ...defaultOptions, ...options };

        document.addEventListener('DOMContentLoaded', () => {

            const errorElement = document.getElementById('card-errors');
            const showError = (message) => {
                errorElement.textContent = message;
                errorElement.classList.remove('d-none');
            };
            const clearError = () => {
                errorElement.textContent = '';
                errorElement.classList.add('d-none');
            };
            const getFilteredStyleObject = (element) => {
                if (!element) {
                    return {};
                }
                const computedStyle = window.getComputedStyle(element);
                const styleObject = {};
                const propertiesToInclude = [
                    'margin',
                    'padding',
                    'color',
                    'font-family',
                    'font-size',
                    'line-height',
                    'border-width',
                    'border-style',
                    'border-color'
                ];

                propertiesToInclude.forEach(property => {
                    styleObject[property] = computedStyle.getPropertyValue(property);
                });

                return styleObject;
            };
            const applyStylesToStripeCard = (styles) => {
                return {
                    base: {
                        color: styles.color || 'black',
                        fontSize: styles['font-size'] || '16px',
                        fontFamily: styles['font-family'] || 'Arial, sans-serif',
                        lineHeight: styles['line-height'] || '1.4',
                        border: `${styles['border-width'] || '1px'} ${styles['border-style'] || 'solid'} ${styles['border-color'] || 'black'}`
                    },
                };
            };
            const stripe = Stripe(config.publishableKey);
            const elements = stripe.elements();

            const filteredStyleObject = getFilteredStyleObject(config.nameOnBankCardElement);
            const cardStyles = applyStylesToStripeCard(filteredStyleObject);

            const cardElement = elements.create('card', {
                style: cardStyles
            });
            cardElement.mount(config.cardElement);
            cardElement.on('change', function (event) {
                clearError();
                config.enablePayButtonButton(true);

                if (event.error) {
                    showError(event.error.message);
                }
            });

            config.payButtonElement.addEventListener('click', function (event) {

                if (config.payButtonElement.getAttribute('data-method-name') !== config.processorKey) {
                    return;
                }

                event.preventDefault();

                if (config.nameOnBankCardElement && !config.nameOnBankCardElement.value) {
                    showError(config.invalidNameErrorMessage);
                    return;
                }

                config.enablePayButtonButton(false);

                stripe.createPaymentMethod({
                    type: 'card',
                    card: cardElement,
                    billing_details: {
                        name: config.nameOnBankCardElement.value || '',
                    },
                }).then(function (result) {
                    if (result.error) {
                        showError(result.error.message);
                    } else {
                        fetch(config.stepIntentEndpoint, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                            },
                            body: JSON.stringify({
                                paymentMethodId: result.paymentMethod.id,
                                sessionId: config.sessionId
                            }),
                        }).then((response) => response.json())
                            .then(async (data) => {
                                if (data.error) {
                                    showError(data.error);
                                } else {
                                    const setupResult = await stripe.confirmCardSetup(data.clientSecret);
                                    if (setupResult.error) {
                                        showError(setupResult.error.message);
                                    } else {
                                        const paymentMethodId = setupResult.setupIntent.payment_method;

                                        if (data.processInitialPayment) {
                                            const paymentResponse = await fetch(config.paymentIntentEndpoint, {
                                                method: 'POST',
                                                headers: {
                                                    'Content-Type': 'application/json',
                                                },
                                                body: JSON.stringify({
                                                    customerId: data.customerId,
                                                    paymentMethodId: paymentMethodId,
                                                    sessionId: config.sessionId,
                                                }),
                                            });
                                            const paymentData = await paymentResponse.json();

                                            if (paymentData.error) {
                                                showError(paymentData.error);
                                            } else {
                                                const paymentResult = await stripe.confirmCardPayment(paymentData.clientSecret);
                                                if (paymentResult.error) {
                                                    showError(paymentResult.error.message);
                                                } else {
                                                    // Handle successful payment and schedule the subscriptions
                                                    await createSubscriptions(data.customerId, paymentMethodId);
                                                }
                                            }
                                        } else {
                                            // Skip initial payment and schedule the subscriptions
                                            await createSubscriptions(data.customerId, paymentMethodId);
                                        }
                                    }
                                }
                            });
                    }
                });
            });

            const createSubscriptions = async (customerId, paymentMethodId) => {
                try {
                    const response = await fetch(config.subscriptionEndpoint, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({
                            customerId: customerId,
                            paymentMethodId: paymentMethodId,
                            sessionId: config.sessionId,
                        }),
                    });
                    const subscriptionDataArray = await response.json();

                    if (subscriptionDataArray.error) {
                        showError(subscriptionDataArray.error);
                        return;
                    }

                    for (const subscriptionData of subscriptionDataArray) {
                        if (subscriptionData.status === 'requires_action') {
                            const result = await stripe.confirmCardPayment(subscriptionData.clientSecret);
                            if (result.error) {
                                showError(result.error.message);
                                return;
                            }
                        }
                    }

                    // Dispatch the form submit event after all subscriptions are processed
                    const submitEvent = new Event("submit", { bubbles: true, cancelable: true });
                    config.formElement.dispatchEvent(submitEvent);

                } catch (error) {
                    showError(error.message);
                }
            };
        });
    };

    return {
        initialize: initialize
    };
}();
