/// <reference path="../../../../node_modules/@types/jasmine/index.d.ts" />
import { TestBed, async, ComponentFixture, ComponentFixtureAutoDetect } from '@angular/core/testing';
import { BrowserModule, By } from "@angular/platform-browser";
import { ExchangesComponent } from './exchanges.component';

let component: ExchangesComponent;
let fixture: ComponentFixture<ExchangesComponent>;

describe('exchanges component', () => {
    beforeEach(async(() => {
        TestBed.configureTestingModule({
            declarations: [ ExchangesComponent ],
            imports: [ BrowserModule ],
            providers: [
                { provide: ComponentFixtureAutoDetect, useValue: true }
            ]
        });
        fixture = TestBed.createComponent(ExchangesComponent);
        component = fixture.componentInstance;
    }));

    it('should do something', async(() => {
        expect(true).toEqual(true);
    }));
});