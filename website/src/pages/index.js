import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Translate, {translate} from '@docusaurus/Translate';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <h1 className="hero__title">{siteConfig.title}</h1>
        <p className="hero__subtitle">
          <Translate id="homepage.tagline">The Ultimate Windows Subsystem for Linux Manager</Translate>
        </p>
        <div className={styles.buttons}>
          <Link
            className="button button--secondary button--lg"
            to="/docs/intro">
            <Translate id="homepage.getStarted">Get Started - 5min ⏱️</Translate>
          </Link>
        </div>
      </div>
    </header>
  );
}

const FeatureList = [
  {
    title: <Translate id="feature.gui.title">Modern GUI Dashboard</Translate>,
    description: (
      <Translate id="feature.gui.description">
        A cross-platform graphical interface built with Fyne to manage your WSL distributions 
        visually and effortlessly.
      </Translate>
    ),
  },
  {
    title: <Translate id="feature.install.title">Custom Installation</Translate>,
    description: (
      <Translate id="feature.install.description">
        Install any WSL distro into a custom directory of your choice, bypassing the 
        default system drive limitations. Move instances easily.
      </Translate>
    ),
  },
  {
    title: <Translate id="feature.offline.title">Offline Support</Translate>,
    description: (
      <Translate id="feature.offline.description">
        Automatically download and cache offline packages (Appx/AppxBundle) for 
        Ubuntu, Debian, Kali, and more. Install without internet.
      </Translate>
    ),
  },
];

function Feature({title, description}) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function Home() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={`Hello from ${siteConfig.title}`}
      description="The Ultimate Windows Subsystem for Linux Manager">
      <HomepageHeader />
      <main>
        <section className={styles.features}>
          <div className="container">
            <div className="row">
              {FeatureList.map((props, idx) => (
                <Feature key={idx} {...props} />
              ))}
            </div>
          </div>
        </section>
      </main>
    </Layout>
  );
}
