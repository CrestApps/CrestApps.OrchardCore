import React from 'react';
import Content from '@theme-original/DocItem/Content';
import ManualBar from '@site/src/components/Manual/ManualBar';

export default function ContentWrapper(props) {
  return (
    <>
      <ManualBar />
      <Content {...props} />
    </>
  );
}
