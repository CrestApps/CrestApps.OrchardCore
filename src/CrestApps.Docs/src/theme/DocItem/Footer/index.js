import React from 'react';
import Footer from '@theme-original/DocItem/Footer';
import ManualCrossLinks from '@site/src/components/Manual/ManualCrossLinks';

export default function FooterWrapper(props) {
  return (
    <>
      <ManualCrossLinks />
      <Footer {...props} />
    </>
  );
}
