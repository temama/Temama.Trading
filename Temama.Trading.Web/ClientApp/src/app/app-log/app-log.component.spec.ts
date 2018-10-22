/// <reference path="../../../../node_modules/@types/jasmine/index.d.ts" />
import { TestBed, async, ComponentFixture, ComponentFixtureAutoDetect } from '@angular/core/testing';
import { BrowserModule, By } from "@angular/platform-browser";
import { AppLogComponent } from './app-log.component';

let component: AppLogComponent;
let fixture: ComponentFixture<AppLogComponent>;

describe('app-log component', () => {
    beforeEach(async(() => {
        TestBed.configureTestingModule({
            declarations: [ AppLogComponent ],
            imports: [ BrowserModule ],
            providers: [
                { provide: ComponentFixtureAutoDetect, useValue: true }
            ]
        });
        fixture = TestBed.createComponent(AppLogComponent);
        component = fixture.componentInstance;
    }));

    it('should do something', async(() => {
        expect(true).toEqual(true);
    }));
});